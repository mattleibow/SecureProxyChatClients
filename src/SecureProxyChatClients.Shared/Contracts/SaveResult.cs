namespace SecureProxyChatClients.Shared.Contracts;

public sealed record SaveResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}
