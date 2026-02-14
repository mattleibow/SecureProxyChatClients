using System.Text.Json;

namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ToolDefinitionDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public JsonElement? Parameters { get; init; }
}
