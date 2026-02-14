namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatResponse
{
    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }
    public UsageDto? Usage { get; init; }
}
