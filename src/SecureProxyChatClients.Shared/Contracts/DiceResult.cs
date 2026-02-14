namespace SecureProxyChatClients.Shared.Contracts;

public sealed record DiceResult
{
    public required IReadOnlyList<int> Rolls { get; init; }
    public required int Total { get; init; }
}
