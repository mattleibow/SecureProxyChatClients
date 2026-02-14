namespace SecureProxyChatClients.Server.Data;

public class ConversationSession
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ConversationMessage> Messages { get; set; } = [];
}
