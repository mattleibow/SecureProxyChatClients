namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatRequest
{
    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }
    public string? SessionId { get; init; }
    public IReadOnlyList<ToolDefinitionDto>? ClientTools { get; init; }
    public ChatOptionsDto? Options { get; init; }
}
