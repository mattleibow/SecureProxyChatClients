namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatMessageDto
{
    public required string Role { get; init; }
    public string? Content { get; init; }
    public string? AuthorName { get; init; }
}
