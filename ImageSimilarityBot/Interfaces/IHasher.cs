namespace ImageSimilarityBot.Interfaces;

public interface IHasher
{
    Task<string> ComputeHashAsync(Stream input, CancellationToken cancellationToken);
}
