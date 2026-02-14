namespace SecureProxyChatClients.Shared.Contracts;

public sealed record SearchResult
{
    public required IReadOnlyList<SearchMatch> Matches { get; init; }
}

public sealed record SearchMatch
{
    public required string Type { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Snippet { get; init; }
}
