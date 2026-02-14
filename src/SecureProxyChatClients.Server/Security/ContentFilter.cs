using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Security;

public sealed class ContentFilter(ILogger<ContentFilter> logger)
{
    // S6: Basic content filtering on output — placeholder for now, log and pass through
    public Shared.Contracts.ChatResponse FilterResponse(Shared.Contracts.ChatResponse response)
    {
        logger.LogDebug("Content filter applied to response with {MessageCount} messages", response.Messages.Count);
        return response;
    }
}
