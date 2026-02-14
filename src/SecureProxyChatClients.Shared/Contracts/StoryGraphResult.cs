namespace SecureProxyChatClients.Shared.Contracts;

public sealed record StoryGraphResult
{
    public required IReadOnlyList<SceneResult> Scenes { get; init; }
    public required IReadOnlyList<CharacterResult> Characters { get; init; }
    public required IReadOnlyList<ConnectionDto> Connections { get; init; }
}

public sealed record ConnectionDto
{
    public required string FromSceneId { get; init; }
    public required string ToSceneId { get; init; }
    public required string Label { get; init; }
}
