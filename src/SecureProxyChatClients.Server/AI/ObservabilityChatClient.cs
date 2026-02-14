using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

/// <summary>
/// IChatClient decorator that emits OpenTelemetry metrics for AI call latency,
/// token usage, and error rates. Wraps any underlying IChatClient.
/// </summary>
public sealed class ObservabilityChatClient : DelegatingChatClient
{
    private static readonly Meter s_meter = new("SecureProxyChatClients.AI");
    private static readonly Counter<long> s_promptTokens = s_meter.CreateCounter<long>("ai.prompt_tokens", "tokens", "Prompt tokens sent");
    private static readonly Counter<long> s_completionTokens = s_meter.CreateCounter<long>("ai.completion_tokens", "tokens", "Completion tokens received");
    private static readonly Histogram<double> s_latency = s_meter.CreateHistogram<double>("ai.request_duration", "ms", "AI request duration");
    private static readonly Counter<long> s_errors = s_meter.CreateCounter<long>("ai.errors", "errors", "AI request errors");
    private static readonly Counter<long> s_requests = s_meter.CreateCounter<long>("ai.requests", "requests", "Total AI requests");

    public ObservabilityChatClient(IChatClient innerClient) : base(innerClient) { }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        s_requests.Add(1);
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            sw.Stop();
            s_latency.Record(sw.Elapsed.TotalMilliseconds);
            RecordUsage(response.Usage);
            return response;
        }
        catch
        {
            sw.Stop();
            s_latency.Record(sw.Elapsed.TotalMilliseconds);
            s_errors.Add(1);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        s_requests.Add(1);
        var sw = Stopwatch.StartNew();

        ChatResponseUpdate? lastUpdate = null;
        bool errored = false;

        IAsyncEnumerable<ChatResponseUpdate> stream;
        try
        {
            stream = base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }
        catch
        {
            sw.Stop();
            s_latency.Record(sw.Elapsed.TotalMilliseconds);
            s_errors.Add(1);
            throw;
        }

        await foreach (var update in stream)
        {
            lastUpdate = update;
            yield return update;
        }

        sw.Stop();
        s_latency.Record(sw.Elapsed.TotalMilliseconds);

        if (!errored && lastUpdate?.Contents is { Count: > 0 })
        {
            var usage = lastUpdate.Contents.OfType<UsageContent>().FirstOrDefault();
            if (usage?.Details is { } details)
            {
                RecordUsage(details);
            }
        }
    }

    private static void RecordUsage(UsageDetails? usage)
    {
        if (usage is null) return;
        if (usage.InputTokenCount is > 0)
            s_promptTokens.Add(usage.InputTokenCount.Value);
        if (usage.OutputTokenCount is > 0)
            s_completionTokens.Add(usage.OutputTokenCount.Value);
    }
}
