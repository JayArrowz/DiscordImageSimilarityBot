using ImageSimilarityBot.Model;
using Microsoft.Extensions.Options;
using NetCord;
using OpenAI;
using OpenAI.Embeddings;
using System.Text.Json.Serialization;

public class VllmEmbeddingService : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly Uri? _embeddingsUri;

    public VllmEmbeddingService(IOptions<AIConfig> aiConfig)
    {
        var baseUrl = aiConfig.Value.ApiUrl;
        var apiKey = aiConfig.Value.ApiKey;
        _model = aiConfig.Value.Model;
        var root = baseUrl!.TrimEnd('/');                 // e.g. http://localhost:8000/v1
        _embeddingsUri = new Uri($"{root}/embeddings");
        _http = new HttpClient();

        if (!string.IsNullOrEmpty(apiKey))
        {
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    public async Task<float[]> EmbedAsync(Stream attachment, string contentType, CancellationToken ct = default)
    {
        if (_embeddingsUri is null)
            throw new InvalidOperationException(
                "Image embedding requires a vLLM-compatible baseUrl; the OpenAI SDK/endpoint can't do multimodal embeddings.");
        using var ms = new MemoryStream();
        await attachment.CopyToAsync(ms, ct);
        var dataUri = $"data:{contentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        var body = new
        {
            model = _model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = dataUri } }
                    }
                }
            },
            encoding_format = "float"
        };

        using var resp = await _http!.PostAsJsonAsync(_embeddingsUri, body, ct);
        resp.EnsureSuccessStatusCode();

        var parsed = await resp.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        var vec = parsed?.Data?.FirstOrDefault()?.Embedding
                  ?? throw new InvalidOperationException("No embedding returned.");

        return vec;
    }

    public async Task<float[]> EmbedAsync(Attachment attachment, CancellationToken ct = default)
    {
        if (_embeddingsUri is null)
            throw new InvalidOperationException(
                "Image embedding requires a vLLM-compatible baseUrl; the OpenAI SDK/endpoint can't do multimodal embeddings.");

        var body = new
        {
            model = _model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = attachment.ProxyUrl } }
                    }
                }
            },
            encoding_format = "float"
        };

        using var resp = await _http!.PostAsJsonAsync(_embeddingsUri, body, ct);
        resp.EnsureSuccessStatusCode();

        var parsed = await resp.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        var vec = parsed?.Data?.FirstOrDefault()?.Embedding
                  ?? throw new InvalidOperationException("No embedding returned.");

        return vec;
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")] public List<EmbeddingItem>? Data { get; set; }
    }
    private sealed class EmbeddingItem
    {
        [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
        [JsonPropertyName("index")] public int Index { get; set; }
    }
}