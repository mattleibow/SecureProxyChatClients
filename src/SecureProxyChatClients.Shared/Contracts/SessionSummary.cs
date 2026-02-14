namespace SecureProxyChatClients.Shared.Contracts;

public sealed record SessionSummary
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
