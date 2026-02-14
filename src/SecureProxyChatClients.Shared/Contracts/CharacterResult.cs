namespace SecureProxyChatClients.Shared.Contracts;

public sealed record CharacterResult
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Backstory { get; init; }
    public required IReadOnlyList<string> Traits { get; init; }
}
