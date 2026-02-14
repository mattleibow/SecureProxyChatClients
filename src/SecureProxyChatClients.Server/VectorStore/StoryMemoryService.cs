using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace SecureProxyChatClients.Server.VectorStore;

public interface IStoryMemoryService
{
    Task StoreMemoryAsync(string userId, string sessionId, string content, string memoryType, float[]? embedding = null, CancellationToken ct = default);
    Task<IReadOnlyList<StoryMemory>> SearchAsync(string userId, float[] queryEmbedding, int limit = 5, CancellationToken ct = default);
    Task<IReadOnlyList<StoryMemory>> GetRecentMemoriesAsync(string userId, int limit = 10, CancellationToken ct = default);
}

public sealed class PgVectorStoryMemoryService(VectorDbContext db, ILogger<PgVectorStoryMemoryService> logger) : IStoryMemoryService
{
    public async Task StoreMemoryAsync(
        string userId, string sessionId, string content, string memoryType,
        float[]? embedding = null, CancellationToken ct = default)
    {
        var memory = new StoryMemory
        {
            UserId = userId,
            SessionId = sessionId,
            Content = content,
            MemoryType = memoryType,
            Embedding = embedding is not null ? new Vector(embedding) : null,
        };

        db.StoryMemories.Add(memory);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Stored {MemoryType} memory for user {UserId} (length: {Length})",
            memoryType, userId, content.Length);
    }

    public async Task<IReadOnlyList<StoryMemory>> SearchAsync(
        string userId, float[] queryEmbedding, int limit = 5, CancellationToken ct = default)
    {
        var queryVector = new Vector(queryEmbedding);

        // Use raw SQL for cosine distance (<=>) to avoid EF extension compatibility issues
        var results = await db.StoryMemories
            .FromSqlInterpolated(
                $"""
                SELECT * FROM "StoryMemories"
                WHERE "UserId" = {userId} AND "Embedding" IS NOT NULL
                ORDER BY "Embedding" <=> {queryVector}
                LIMIT {limit}
                """)
            .ToListAsync(ct);

        return results;
    }

    public async Task<IReadOnlyList<StoryMemory>> GetRecentMemoriesAsync(
        string userId, int limit = 10, CancellationToken ct = default)
    {
        return await db.StoryMemories
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}

/// <summary>
/// Fallback when PostgreSQL/pgvector is not available (dev/test).
/// Stores memories in-memory without vector search capability.
/// </summary>
public sealed class InMemoryStoryMemoryService(ILogger<InMemoryStoryMemoryService> logger) : IStoryMemoryService
{
    private readonly List<StoryMemory> _memories = [];

    public Task StoreMemoryAsync(
        string userId, string sessionId, string content, string memoryType,
        float[]? embedding = null, CancellationToken ct = default)
    {
        _memories.Add(new StoryMemory
        {
            UserId = userId,
            SessionId = sessionId,
            Content = content,
            MemoryType = memoryType,
        });

        logger.LogDebug("Stored in-memory {MemoryType} for user {UserId}", memoryType, userId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoryMemory>> SearchAsync(
        string userId, float[] queryEmbedding, int limit = 5, CancellationToken ct = default)
    {
        // Without embeddings, return recent memories for the user
        IReadOnlyList<StoryMemory> results = _memories
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<StoryMemory>> GetRecentMemoriesAsync(
        string userId, int limit = 10, CancellationToken ct = default)
    {
        IReadOnlyList<StoryMemory> results = _memories
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }
}
