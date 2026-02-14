using Microsoft.EntityFrameworkCore;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Data;

public sealed class EfConversationStore(AppDbContext db) : IConversationStore
{
    public async Task<string> CreateSessionAsync(string userId, CancellationToken ct = default)
    {
        var session = new ConversationSession
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.ConversationSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task AppendMessagesAsync(string sessionId, IReadOnlyList<ChatMessageDto> messages, CancellationToken ct = default)
    {
        // Note: In a high-concurrency production scenario, this read-then-write pattern
        // could lead to sequence number collisions. Consider using optimistic concurrency
        // or a database-generated sequence if strict ordering guarantees are required.
        int maxSeq = await db.ConversationMessages
            .Where(m => m.SessionId == sessionId)
            .Select(m => (int?)m.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        var now = DateTime.UtcNow;
        foreach (var msg in messages)
        {
            maxSeq++;
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                Role = msg.Role,
                Content = msg.Content,
                AuthorName = msg.AuthorName,
                CreatedAt = now,
                SequenceNumber = maxSeq,
            });
        }

        // Update session timestamp and title
        var session = await db.ConversationSessions.FindAsync([sessionId], ct);
        if (session is not null)
        {
            session.UpdatedAt = now;
            if (session.Title is null)
            {
                var firstUserMsg = messages.FirstOrDefault(m => m.Role == "user");
                if (firstUserMsg?.Content is { Length: > 0 } content)
                {
                    session.Title = content.Length > 80 ? content[..80] + "…" : content;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(string sessionId, CancellationToken ct = default)
    {
        return await db.ConversationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SequenceNumber)
            .Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                AuthorName = m.AuthorName,
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetUserSessionsAsync(string userId, CancellationToken ct = default)
    {
        return await db.ConversationSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SessionSummary
            {
                Id = s.Id,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<string?> GetSessionOwnerAsync(string sessionId, CancellationToken ct = default)
    {
        return await db.ConversationSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.UserId)
            .FirstOrDefaultAsync(ct);
    }
}
