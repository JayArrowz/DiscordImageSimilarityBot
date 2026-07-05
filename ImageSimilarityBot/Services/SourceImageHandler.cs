using ImageSimilarityBot.Interfaces;
using ImageSimilarityBot.Model;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ImageSimilarityBot.Services;

public class SourceImageHandler
{
    private string _sourceImageDir;
    private VllmEmbeddingService _vllmEmbeddingService;
    private readonly ImageSimilarityContext _dbContext;
    private readonly ILogger<SourceImageHandler> _logger;
    private readonly HttpClient _httpClient;
    private IHasher _hasher;

    public SourceImageHandler(IConfiguration configuration,
        IHasher hasher,
        VllmEmbeddingService vllmEmbeddingService,
        ImageSimilarityContext dbContext,
        ILogger<SourceImageHandler> logger,
        HttpClient httpClient)
    {
        _sourceImageDir = configuration["SourceImageDir"] ?? throw new ArgumentNullException("SourceImageDir");
        _vllmEmbeddingService = vllmEmbeddingService;
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = httpClient;
        _hasher = hasher;
    }

    public async Task HashAndEmbedSourceImagesAsync(CancellationToken cts)
    {
        var imageFiles = Directory.GetFiles(_sourceImageDir);
        await AddImagesAsync(imageFiles, cts, true);
    }

    public async Task<string> AddOrUpdateImageAsync(string url, bool? bannable, double? threashold, CancellationToken cts)
    {
        try
        {
            var uri = new Uri(url);
            var headResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cts);
            headResponse.EnsureSuccessStatusCode();
            var contentType = headResponse.Content.Headers.ContentType?.MediaType ?? throw new InvalidOperationException("Failed to determine content type.");
            if (contentType.StartsWith("image/"))
            {
                using var response = await _httpClient.GetAsync(url, cts);
                string filename = System.IO.Path.GetFileName(uri.LocalPath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filename) + ".json";
                var fullPath = Path.Combine(_sourceImageDir, filename);
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync(cts);


                using var fs = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                await stream.CopyToAsync(fs, cts);
                await fs.FlushAsync(cts);
                fs.Close();

                if(bannable.HasValue || threashold.HasValue)
                {
                    var metadata = new SourceImageMetadata
                    {
                        Bannable = bannable,
                        SimilarityThreshold = threashold
                    };
                    var metadataPath = Path.Combine(_sourceImageDir, fileNameWithoutExt);
                    await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cts);
                }
            }
            else
            {
                _logger.LogWarning("Skipping non-image content type: {contentType} for URL: {url}", contentType, url);
                return $"Skipping non-image content type: {contentType} for URL: {url}";
            }

            await HashAndEmbedSourceImagesAsync(cts);
        } catch(Exception e)
        {
            _logger.LogError(e, "Error processing image url: {Path}", url);
            return "Error processing image url: " + url + " Err Message: "+e.Message;
        }

        return $"Added image {url} successfully.";
    }

    private async Task AddImagesAsync(string[] imageFiles, CancellationToken cts, bool deleteExisting = false)
    {
        var contentProvider = new FileExtensionContentTypeProvider();
        var markHistoriesAsStale = false;
        List<string> imagesAdded = new();
        foreach (var imageFile in imageFiles)
        {
            try
            {
                var metadataPath = Path.Combine(_sourceImageDir, Path.GetFileNameWithoutExtension(imageFile) + ".json");
                var metadata = File.Exists(metadataPath) ? JsonSerializer.Deserialize<SourceImageMetadata>(File.ReadAllText(metadataPath)) : null;
                using var fileStream = File.OpenRead(imageFile);
                var existingSrcImage = await _dbContext.SourceImages.FirstOrDefaultAsync(si => si.Path == imageFile, cts);
                var sha256Hash = await _hasher.ComputeHashAsync(fileStream, cts);

                if (existingSrcImage != null && (existingSrcImage.Hash == sha256Hash && existingSrcImage.SimilarityThreshold == metadata?.SimilarityThreshold && existingSrcImage.Bannable == metadata?.Bannable))
                {
                    imagesAdded.Add(imageFile);
                    continue;
                }
                if (!contentProvider.TryGetContentType(Path.GetFileName(imageFile), out var contentType))
                {
                    throw new InvalidOperationException("Cannot find content type for " + imageFile);
                }

                if (!contentType.StartsWith("image/"))
                {
                    _logger.LogWarning("File {Path} is not an image, skipping.", imageFile);
                    continue;
                }

                if (existingSrcImage == null)
                {
                    var embedding = await _vllmEmbeddingService.EmbedAsync(fileStream, contentType, cts);
                    existingSrcImage = new Model.SourceImage
                    {
                        Path = imageFile,
                        Hash = sha256Hash,
                        Embedding = new Pgvector.Vector(embedding),
                        SimilarityThreshold = metadata?.SimilarityThreshold,
                        Bannable = metadata?.Bannable
                    };
                    _dbContext.SourceImages.Add(existingSrcImage);
                    markHistoriesAsStale = true;
                    _logger.LogInformation("Added new source image: {Path} with similarity threshold: {Threshold}, bannable: {Bannable}", imageFile, existingSrcImage.SimilarityThreshold, existingSrcImage.Bannable);
                    imagesAdded.Add(imageFile);
                    continue;
                }

                existingSrcImage.Bannable = metadata?.Bannable;

                if (existingSrcImage.SimilarityThreshold != metadata?.SimilarityThreshold)
                {
                    existingSrcImage.SimilarityThreshold = metadata?.SimilarityThreshold;
                    markHistoriesAsStale = true;
                }

                if (existingSrcImage.Hash != sha256Hash)
                {
                    existingSrcImage.Embedding = new Pgvector.Vector(await _vllmEmbeddingService.EmbedAsync(fileStream, contentType, cts));
                    markHistoriesAsStale = true;
                }

                existingSrcImage.Hash = sha256Hash;
                _dbContext.SourceImages.Update(existingSrcImage);
                _logger.LogInformation("Updated existing source image: {Path} with similarity threshold: {Threshold}, bannable: {Bannable}", imageFile, existingSrcImage.SimilarityThreshold, existingSrcImage.Bannable);
                imagesAdded.Add(imageFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image file: {Path}", imageFile);
            }
        }
        await _dbContext.SaveChangesAsync();


        var deleted = await _dbContext.SourceImages.Where(si => !imagesAdded.Contains(si.Path)).ExecuteDeleteAsync();
        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} source images that no longer exist in the directory.", deleted);
            markHistoriesAsStale = true;
        }

        if (markHistoriesAsStale)
        {
            _logger.LogInformation("New source images added or updated, marking attachment histories as stale.");
            await _dbContext.AttachmentHistories.ExecuteUpdateAsync(e => e.SetProperty(v => v.Stale, v => true));
        }
    }
}
