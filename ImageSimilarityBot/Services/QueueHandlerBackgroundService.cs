using ImageSimilarityBot.Interfaces;
using ImageSimilarityBot.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using Pgvector.EntityFrameworkCore;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace ImageSimilarityBot.Services;

public class QueueHandlerBackgroundService : BackgroundService, IDisposable
{
    private readonly IMessageQueue _messageQueue;
    private readonly HttpClient _http;
    private readonly IHasher _hasher;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly VllmEmbeddingService _embeddingService;
    private readonly IOptions<AIConfig> _aiConfig;
    private readonly ILogger<QueueHandlerBackgroundService> _logger;
    private const string UrlPattern = @"https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,63}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&\/=]*)";

    public QueueHandlerBackgroundService(ILogger<QueueHandlerBackgroundService> logger, IMessageQueue messageQueue, HttpClient http,
        IHasher hasher, IServiceScopeFactory serviceScopeFactory, VllmEmbeddingService embeddingService, IOptions<AIConfig> aiConfig)
    {
        _logger = logger;
        _messageQueue = messageQueue;
        _http = http;
        _hasher = hasher;
        _serviceScopeFactory = serviceScopeFactory;
        _embeddingService = embeddingService;
        _aiConfig = aiConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "QueueHandler Hosted Service running.");
        await ProcessQueueAsync(stoppingToken);
    }
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "QueueHandler Hosted Service is stopping.");

        await base.StopAsync(stoppingToken);
    }

    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await _messageQueue.DequeueMessageAsync(cancellationToken);
                if (message == null)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                _logger.LogInformation("Processing message: {id}", message.Id);

                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ImageSimilarityContext>();

                // Process the attachments
                await ProcessAttachmentsAsync(dbContext, message, cancellationToken);
                await ProcessContentAsync(dbContext, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue.");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task ProcessContentAsync(ImageSimilarityContext dbContext, Message message, CancellationToken cancellationToken)
    {
        var matches = Regex.Matches(message.Content, UrlPattern);
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                try
                {
                    var url = match.Value;
                    var headResponse = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cancellationToken);
                    headResponse.EnsureSuccessStatusCode();
                    var contentType = headResponse.Content.Headers.ContentType?.MediaType ?? throw new InvalidOperationException("Failed to determine content type.");
                    await ProcessAsync(dbContext, message, url, url, contentType, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing image URL in message content: {url}", match.Value);
                }
            }
        }
    }

    public async Task ProcessAttachmentsAsync(ImageSimilarityContext dbContext, Message message, CancellationToken cancellationToken = default)
    {
        var attachments = message.Attachments.Where(t => t.ContentType?.StartsWith("image/") ?? false);
        foreach (var attachment in attachments)
        {
            try
            {
                await ProcessAsync(dbContext, message, attachment.ProxyUrl, attachment.Url, attachment.ContentType!, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image attachment: {url}", attachment.ProxyUrl);
            }
        }
    }

    public async Task ProcessAsync(ImageSimilarityContext dbContext, Message message, string urlToProcess, string originalUrl, string contentType, CancellationToken cancellationToken = default)
    {
        if(!contentType.StartsWith("image/"))
        {
            _logger.LogWarning("Skipping non-image content type: {contentType} for URL: {url}", contentType, urlToProcess);
            return;
        }

        using var response = await _http.GetAsync(urlToProcess, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var hash = await _hasher.ComputeHashAsync(stream, cancellationToken);
        var existingImage = await dbContext.AttachmentHistories.FirstOrDefaultAsync(a =>
        a.Hash == hash ||
        (a.OriginalUrl != null && a.OriginalUrl == originalUrl) ||
        (a.ProxyUrl != null && a.ProxyUrl == urlToProcess), cancellationToken);
        var hasExistingImage = existingImage != null;
        if (existingImage != null && existingImage.Blocked && !existingImage.Stale)
        {

            await message.ReplyAsync(new NetCord.Rest.ReplyMessageProperties
            {
                Content = "Image has been blocked"
            });
            await ActionMessageAsync(dbContext, message, existingImage, cancellationToken);
        }
        else if (existingImage == null || existingImage.Stale)
        {
            var embedding = existingImage?.Embedding ?? new Pgvector.Vector(await _embeddingService.EmbedAsync(stream, contentType!, cancellationToken));
            existingImage ??= new AttachmentHistory
            {
                OriginalUrl = originalUrl,
                Hash = hash,
                ProxyUrl = urlToProcess,
                Embedding = embedding,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var vector = existingImage.Embedding;
            var results = await dbContext
                .SourceImages
               .OrderBy(c => c.Embedding!.CosineDistance(vector))
               .Take(1)
               .Select(c => new { SourceImage = c, Distance = 1.0 - c.Embedding!.CosineDistance(vector) })
               .FirstOrDefaultAsync(cancellationToken);
            
            existingImage.Stale = false;
            var st = results?.SourceImage?.SimilarityThreshold ?? _aiConfig.Value.ActionableThreshold;
            existingImage.Blocked = results != null && results.Distance >= (st);
            existingImage.SourceImageId = results?.SourceImage?.Id;
            if (hasExistingImage)
            {
                dbContext.AttachmentHistories.Update(existingImage);
            }
            else
            {
                dbContext.AttachmentHistories.Add(existingImage);
            }


            await dbContext.SaveChangesAsync(cancellationToken);
            if (existingImage.Blocked)
            {
                await message.ReplyAsync(new NetCord.Rest.ReplyMessageProperties
                {
                    Content = $"Image has been blocked, Similarity Threshold: {st}, This image is {results.Distance} / 1 similar to {results.SourceImage.Path}"
                });
                await ActionMessageAsync(dbContext, message, existingImage, cancellationToken);
            }
        }
    }

    private async Task ActionMessageAsync(ImageSimilarityContext dbContext, Message message, AttachmentHistory attachment, CancellationToken cancellationToken)
    {
        await message.DeleteAsync();
        _logger.LogInformation("Deleted message {id} due to blocked image attachment: {url}", message.Id, attachment.ProxyUrl);
        var isBannable = _aiConfig.Value.Bannable;
        if (isBannable || attachment.SourceImageId.HasValue)
        {
            var sourceImage = isBannable ? null : await dbContext.SourceImages.FindAsync([attachment.SourceImageId.Value], cancellationToken);
            if (isBannable || (sourceImage != null && sourceImage.Bannable.GetValueOrDefault()))
            {
                await message.Guild!.BanUserAsync(message.Author.Id, 0, new NetCord.Rest.RestRequestProperties
                {
                    AuditLogReason = $"Blocked image matched banned source image: {attachment.ProxyUrl}"
                }, cancellationToken: cancellationToken);
                _logger.LogInformation("Blocked image matched banned source image: {path}", attachment.ProxyUrl);
            }
        }
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}
