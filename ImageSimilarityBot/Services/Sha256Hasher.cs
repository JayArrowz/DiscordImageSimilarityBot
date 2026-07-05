using ImageSimilarityBot.Interfaces;

namespace ImageSimilarityBot.Services;

public class Sha256Hasher : IHasher
{
    public Task<string> ComputeHashAsync(Stream input, CancellationToken cancellationToken)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(input);
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        input.Position = 0;
        return Task.FromResult(hashString);
    }
}
