namespace SecureProxyChatClients.Server.Data;

public class ConversationMessage
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SequenceNumber { get; set; }

    public ConversationSession Session { get; set; } = null!;
}
