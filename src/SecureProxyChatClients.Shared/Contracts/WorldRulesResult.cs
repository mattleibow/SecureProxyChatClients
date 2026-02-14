namespace SecureProxyChatClients.Shared.Contracts;

public sealed record WorldRulesResult
{
    public required IReadOnlyList<WorldRule> Rules { get; init; }
}

public sealed record WorldRule
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}
