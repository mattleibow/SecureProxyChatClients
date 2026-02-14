namespace SecureProxyChatClients.Shared.Contracts;

public sealed record TwistResult
{
    public required string Description { get; init; }
    public required int ImpactLevel { get; init; }
    public required IReadOnlyList<string> AffectedCharacters { get; init; }
}
