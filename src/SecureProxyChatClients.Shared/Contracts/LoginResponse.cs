namespace SecureProxyChatClients.Shared.Contracts;

public sealed record LoginResponse
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public int ExpiresIn { get; init; }
}
