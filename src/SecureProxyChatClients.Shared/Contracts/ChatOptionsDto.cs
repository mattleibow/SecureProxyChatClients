using System.Text.Json;

namespace SecureProxyChatClients.Shared.Contracts;

public sealed record ChatOptionsDto
{
    public bool Streaming { get; init; }
    public JsonElement? ResponseFormat { get; init; }
}
