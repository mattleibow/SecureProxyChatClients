using System.Text.Json;

namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatStreamEvent
{
    public required string Type { get; init; }
    public string? Delta { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public JsonElement? Arguments { get; init; }
    public UsageDto? Usage { get; init; }
}
