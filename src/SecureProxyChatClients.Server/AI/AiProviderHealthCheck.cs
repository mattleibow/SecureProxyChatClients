using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecureProxyChatClients.Server.AI;

/// <summary>
/// Health check that verifies the AI provider is reachable by sending a minimal request.
/// </summary>
public sealed class AiProviderHealthCheck(IChatClient chatClient, ILogger<AiProviderHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, "ping"),
            };

            var options = new ChatOptions { MaxOutputTokens = 1 };
            var response = await chatClient.GetResponseAsync(messages, options, cts.Token);

            return response is not null
                ? HealthCheckResult.Healthy("AI provider is responsive")
                : HealthCheckResult.Degraded("AI provider returned null response");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("AI provider timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI provider health check failed");
            return HealthCheckResult.Unhealthy("AI provider unreachable", ex);
        }
    }
}
