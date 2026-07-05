using ImageSimilarityBot.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class ImageSimilarityContext : DbContext
{
    private readonly IOptions<AIConfig> _aiConfig;

    public ImageSimilarityContext(DbContextOptions<ImageSimilarityContext> opts, IOptions<AIConfig> aiConfig) : base(opts)
    {
        _aiConfig = aiConfig;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");


        modelBuilder.Entity<SourceImage>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<SourceImage>()
            .Property(x => x.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<SourceImage>()
            .HasMany(t => t.AttachmentHistories)
            .WithOne(a => a.SourceImage)
            .HasForeignKey(a => a.SourceImageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SourceImage>()
            .Property(si => si.Embedding)
            .HasColumnType($"vector({_aiConfig.Value.VectorDimensions})");



        modelBuilder.Entity<AttachmentHistory>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<AttachmentHistory>()
            .Property(x => x.Id)
            .ValueGeneratedOnAdd();
        modelBuilder.Entity<AttachmentHistory>()
            .Property(si => si.Embedding)
            .HasColumnType($"vector({_aiConfig.Value.VectorDimensions})");

        modelBuilder.Entity<AttachmentHistory>()
            .HasIndex(a => a.Hash)
            .HasDatabaseName("IX_AttachmentHistory_Hash");

        modelBuilder.Entity<AttachmentHistory>()
            .HasIndex(a => a.OriginalUrl)
            .HasDatabaseName("IX_AttachmentHistory_Url");

        modelBuilder.Entity<SourceImage>()
            .HasIndex(a => a.Hash)
            .HasDatabaseName("IX_SourceImage_Hash");


        base.OnModelCreating(modelBuilder);
    }

    public DbSet<SourceImage> SourceImages { get; set; } = null!;
    public DbSet<AttachmentHistory> AttachmentHistories { get; set; } = null!;
}