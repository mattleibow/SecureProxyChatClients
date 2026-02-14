using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace SecureProxyChatClients.Server.VectorStore;

public class VectorDbContext(DbContextOptions<VectorDbContext> options) : DbContext(options)
{
    public DbSet<StoryMemory> StoryMemories => Set<StoryMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<StoryMemory>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Embedding).HasColumnType("vector(384)");
            e.HasIndex(m => m.UserId);
            e.HasIndex(m => m.SessionId);
        });
    }
}

public class StoryMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MemoryType { get; set; } = "event"; // event, character, location, item, lore
    public string? Tags { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
