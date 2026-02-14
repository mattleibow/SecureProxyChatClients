namespace SecureProxyChatClients.Shared.Contracts;

public sealed record SceneResult
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Characters { get; init; }
    public required string Location { get; init; }
    public required string Mood { get; init; }
}
