using Pgvector;

namespace ImageSimilarityBot.Model;

public class SourceImage : IHashable, IEmbeddable
{
    public int Id { get; set; }
    public string Path { get; set; }
    public Vector Embedding { get; set; }
    public string Hash { get; set; }
    public double? SimilarityThreshold { get; set; }
    public bool? Bannable { get; set; }
    public virtual ICollection<AttachmentHistory> AttachmentHistories { get; set; } = new List<AttachmentHistory>();
}
