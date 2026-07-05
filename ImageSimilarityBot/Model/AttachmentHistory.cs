using Pgvector;

namespace ImageSimilarityBot.Model;

public class AttachmentHistory : IEmbeddable, IHashable
{
    public int Id { get; set; }
    public string OriginalUrl { get; set; }
    public string ProxyUrl { get; set; }
    public string Hash { get; set; }
    public Vector Embedding { get; set; }
    public bool Blocked { get; set; }
    public int? SourceImageId { get; set; }
    public virtual SourceImage? SourceImage { get; set; }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    public bool Stale { get; set; }
}
