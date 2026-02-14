using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Data;

public interface IConversationStore
{
    Task<string> CreateSessionAsync(string userId, CancellationToken ct = default);
    Task AppendMessagesAsync(string sessionId, IReadOnlyList<ChatMessageDto> messages, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSummary>> GetUserSessionsAsync(string userId, CancellationToken ct = default);
    Task<string?> GetSessionOwnerAsync(string sessionId, CancellationToken ct = default);
}
