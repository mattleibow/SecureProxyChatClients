namespace SecureProxyChatClients.Shared.Contracts;

public sealed record UsageDto
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
}
