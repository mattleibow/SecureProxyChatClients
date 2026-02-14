# Architecture Review: SecureProxyChatClients

## Executive Summary

The `SecureProxyChatClients` reference sample demonstrates a strong foundation for a secure augmenting proxy using .NET 10, Aspire, and Microsoft.Extensions.AI. It correctly implements the "Backend for Frontend" (BFF) pattern, keeping credentials secure while enabling a rich client experience.

However, as a production reference sample, it lacks several critical "Day 2" operational features—specifically around **observability**, **resilience pipelines for AI calls**, **global error handling**, and **automated audit trails**.

## 1. Resilience Patterns

**Current State**: Uses Aspire's default HTTP resilience for `HttpClient`, but `AzureOpenAIClient` and the AI service registration do not explicitly configure a retry policy for AI-specific errors (429, 5xx) or timeouts on the `IChatClient` itself.

**Gap**: `AiServiceExtensions.cs` manually instantiates clients without a resilience pipeline.

**Recommendation**: Use `ChatClientBuilder` (if available in the preview version used) or manual decoration to apply a Polly pipeline.

### Code to Add (`src/SecureProxyChatClients.Server/AI/AiServiceExtensions.cs`)

Refactor `AddAiServices` to use a resilience wrapper:

```csharp
using Microsoft.Extensions.Http.Resilience;
using Polly;

// ... inside AddAiServices
// Define a resilience pipeline for AI calls
services.AddResiliencePipeline("ai-pipeline", builder =>
{
    builder.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder().Handle<Exception>() // Refine this
    });
    builder.AddTimeout(TimeSpan.FromSeconds(30));
});

// ... when registering the client, wrap it (conceptually)
// Note: MEAI might have a specific builder. If not, creating a DelegatingChatClient is best.
```

## 2. Observability

**Current State**: Basic OpenTelemetry via Aspire.

**Gap**: No custom metrics for **Token Usage** (input/output/total) or **AI Latency**. These are critical for cost management.

**Recommendation**: Add a decorator `ObservabilityChatClient`.

### Code to Add (`src/SecureProxyChatClients.Server/AI/ObservabilityChatClient.cs`)

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;

public class ObservabilityChatClient(IChatClient inner, IMeterFactory meterFactory) : DelegatingChatClient(inner)
{
    private readonly Counter<long> _inputTokenCounter = meterFactory.Create("ai.tokens.input").CreateCounter<long>("ai.tokens.input");
    private readonly Counter<long> _outputTokenCounter = meterFactory.Create("ai.tokens.output").CreateCounter<long>("ai.tokens.output");
    private readonly Histogram<double> _latencyHistogram = meterFactory.Create("ai.latency").CreateHistogram<double>("ai.latency");

    public override async Task<ChatResponse> GetResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        long start = Stopwatch.GetTimestamp();
        var response = await base.GetResponseAsync(chatMessages, options, cancellationToken);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        _latencyHistogram.Record(elapsed.TotalSeconds);

        if (response.Usage != null)
        {
            _inputTokenCounter.Add(response.Usage.InputTokenCount ?? 0);
            _outputTokenCounter.Add(response.Usage.OutputTokenCount ?? 0);
        }

        return response;
    }
}
```

## 3. Configuration

**Current State**: Good usage of `IConfiguration`.

**Gap**: Missing validation. If `AI:ApiKey` is missing, it throws at startup (good), but using `OptionsValidation` is more idiomatic.

**Recommendation**: Bind options with validation.

### Code to Add (`src/SecureProxyChatClients.Server/Program.cs`)

```csharp
builder.Services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection("AI"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## 4. Error Handling

**Current State**: Manual `try/catch` or return `Results.BadRequest` in endpoints.

**Gap**: No global exception handler. Unhandled exceptions (e.g., DB failure) will return 500 without structure.

**Recommendation**: Add `ProblemDetails` and `ExceptionHandler`.

### Code to Add (`src/SecureProxyChatClients.Server/Program.cs`)

```csharp
// Add services
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add middleware
app.UseExceptionHandler();
```

### Code to Add (`src/SecureProxyChatClients.Server/Infrastructure/GlobalExceptionHandler.cs`)

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = exception.Message // Don't expose this in prod!
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

## 5. API Design

**Current State**: Minimal APIs are used effectively.

**Gap**: `ChatEndpoints.cs` is monolithic.

**Recommendation**: Extract the "Orchestration" logic (validation -> session -> system prompt -> tool loop) into a `ChatOrchestrator` service. This keeps the Endpoint clean and testable.

## 6. Dependency Injection

**Current State**: Good.

## 7. Middleware Pipeline

**Current State**: Standard.

**Gap**: Missing **Request/Response Audit Logging**. For a "Secure Proxy", logging *what* was sent to the AI (sanitized) is a key requirement.

**Recommendation**: Add middleware to log request bodies (carefully).

## 8. Testing

**Current State**: High unit test count.

**Gap**: `ChatEndpoints` integration tests likely don't cover the *streaming* tool call edge cases because the implementation is missing/incomplete in `HandleChatStreamAsync`.

## 9. Documentation

**Current State**: Good README.

**Gap**: No inline code comments for complex logic (e.g., the manual tool loop in `ChatEndpoints`).

## 10. Performance

**Current State**: `EfConversationStore` uses `AppendMessagesAsync`.

**Gap**: If chat history grows long, fetching the *entire* history for every request (implied by `GetHistoryAsync` usage or context loading) is slow.
**Recommendation**: Implement a "sliding window" or "summary" mechanism in `SystemPromptService` to only load the last N messages + summary.

## 11. Code Quality

**Current State**: High.

## 12. Aspire Orchestration

**Current State**: Correctly configured.

## 13. MEAI (Microsoft.Extensions.AI)

**Current State**: Uses the abstractions well.

**Gap**: `HandleChatAsync` manually implements the tool loop. MEAI provides `UseFunctionInvocation`.
**Discussion**: The manual loop is likely needed to support *Client Tools* (returning control to the client).
**Recommendation**: Comment this explicitly. "We cannot use default FunctionInvocation because we need to support hybrid server/client tool execution."

## 14. Security

**Current State**: Token bucket (rate limiting) is fixed window.

**Gap**: "Token bucket" usually implies a specific algorithm, whereas `AddFixedWindowLimiter` is used.
**Recommendation**: Switch to `AddTokenBucketLimiter` for burst handling.

### Code to Add (`src/SecureProxyChatClients.Server/Program.cs`)

```csharp
options.AddTokenBucketLimiter("chat", limiterOptions =>
{
    limiterOptions.TokenLimit = 100;
    limiterOptions.TokensPerPeriod = 10;
    limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
});
```

## Summary of Critical Fixes Required

1.  **Add Global Exception Handler** (`GlobalExceptionHandler.cs`).
2.  **Refactor `ChatEndpoints.cs`** to move logic into `ChatOrchestrator` service.
3.  **Fix Streaming Tool Support**: `HandleChatStreamAsync` does not support tool calls. This is a functional bug in a reference sample claiming to support agents.
4.  **Add Observability Decorator**: Track tokens/cost.
