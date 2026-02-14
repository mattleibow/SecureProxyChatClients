using System.Text.Json;

namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatMessageDto
{
    public required string Role { get; init; }
    public string? Content { get; init; }
    public string? AuthorName { get; init; }
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
}

public sealed record ToolCallDto
{
    public required string CallId { get; init; }
    public required string Name { get; init; }
    public JsonElement? Arguments { get; init; }
}
