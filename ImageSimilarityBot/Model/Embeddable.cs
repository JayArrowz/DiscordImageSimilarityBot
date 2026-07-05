using Pgvector;

namespace ImageSimilarityBot.Model;

public interface IEmbeddable
{
    public Vector Embedding { get; set; }
}
