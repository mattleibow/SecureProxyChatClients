# AI Integration — Providers, Tool Calling, and Streaming

## Overview

The secure proxy uses **Microsoft.Extensions.AI (MEAI)** to abstract AI provider communication behind the `IChatClient` interface. This abstraction enables provider switching without changing application logic, supports multiple tool calling patterns, and integrates streaming via Server-Sent Events (SSE).

## Microsoft.Extensions.AI Abstraction

MEAI provides a unified interface for AI chat interactions. The proxy registers a single `IChatClient` in the dependency injection container, and all endpoints consume it without knowing the underlying provider.

Key benefits of this approach:

- **Provider independence** — Switch between Azure OpenAI, local models, or test doubles via configuration
- **Decorator pattern** — Cross-cutting concerns (observability, logging) wrap the inner client
- **Consistent API** — `CompleteAsync` and `CompleteStreamingAsync` work identically across providers

The `ObservabilityChatClient` decorator wraps every provider, adding OpenTelemetry metrics for request latency, token counts, and error rates.

## Provider Configuration

The `AI:Provider` configuration key selects the active provider. Three providers are included:

| Provider | Configuration Value | Purpose |
|----------|-------------------|---------|
| Fake | `Fake` | Development and testing — returns queue-based deterministic responses |
| Copilot CLI | `CopilotCli` | Local development — invokes an external CLI process |
| Azure OpenAI | `AzureOpenAI` | Production — connects to Azure OpenAI Service |

### Azure OpenAI Setup

The Azure OpenAI provider requires three configuration values:

| Key | Required | Default |
|-----|----------|---------|
| `AI:Endpoint` | Yes | — |
| `AI:ApiKey` | Yes | — |
| `AI:DeploymentName` | No | `gpt-4o` |

The provider creates an `AzureOpenAIClient` with the configured endpoint and API key credential, then obtains a chat client for the specified deployment. All credentials remain server-side — the client application never sees them.

### Fake Provider

The Fake provider is designed for testing and local development without AI costs. It supports:

- **Queue-based responses** — Enqueue specific responses that are returned in order
- **Keyword detection** — Responds to specific patterns in user input
- **Tool call simulation** — Returns simulated tool call requests for testing the tool loop

Integration and E2E tests use `AI:Provider=Fake` to run deterministically without external dependencies.

## Tool Calling Architecture

The proxy supports two categories of tools, each with different trust and execution models.

### Server-Side Tools

Server tools execute entirely on the server. The AI model generates a tool call request, and the server executes it locally without client involvement.

Registered server tools include narrative and game tools:

- **Narrative tools** — Scene generation, character creation, story analysis, plot twist suggestions
- **Game tools** — Dice checks, player movement, inventory management, health/gold/XP modification, NPC generation

Server tools are registered using `AIFunctionFactory.Create`, which reflects over static methods annotated with `[Description]` attributes to produce `AIFunction` instances.

### Client-Side Tools

Client tools execute in the browser. When the AI model requests a tool that is not in the server registry, the proxy returns the tool call to the client for local execution. The client sends the result back, and the proxy continues the AI conversation.

Client tools include:

- Story graph visualization
- Story state search and persistence
- Dice rolling (with local randomness)
- World rules lookup

Client tool names are validated against an allowlist defined in `SecurityOptions.AllowedToolNames`. Any unrecognized tool name is rejected.

### Tool Execution Loop

The server implements a bounded tool execution loop:

1. Send messages to the AI provider
2. If the response contains tool call requests, check the tool registry
3. **Server tool** — Execute locally, append result, repeat from step 1
4. **Client tool** — Return to client for execution
5. **No tools** — Apply content filtering, return final response

The loop is bounded to prevent infinite cycles: 5 rounds for chat endpoints, 8 rounds for play endpoints (which use more tools).

## Streaming with Server-Sent Events

The `/api/chat/stream` and `/api/play/stream` endpoints use SSE to deliver AI responses incrementally.

### SSE Protocol

The response uses standard SSE formatting:

| Header | Value |
|--------|-------|
| `Content-Type` | `text/event-stream` |
| `Cache-Control` | `no-cache` |
| `Connection` | `keep-alive` |

### Event Types

| Event | Payload | Purpose |
|-------|---------|---------|
| `text-delta` | `{ "text": "..." }` | Individual text chunk from the AI |
| `error` | `{ "error": "..." }` | Stream interruption |
| `done` | `{ "sessionId": "..." }` | Completion signal with session identifier |

Each chunk is flushed immediately to minimize latency. The client receives text as the AI generates it, providing a responsive user experience.

## Content Filtering

AI output is sanitized before reaching the client. The content filter removes:

- `<script>` tags
- `<iframe>`, `<object>`, and `<embed>` tags
- Event handler attributes (`onclick`, `onerror`, etc.)
- `javascript:` protocol URIs

Content filtering applies at multiple points:

- **Non-streaming responses** — Filter the complete response before returning
- **Streaming responses** — Filter each chunk as it arrives, then filter the complete concatenated text before persistence

This double-filtering approach ensures that split-across-chunks injection (where `<scr` arrives in one chunk and `ipt>` in the next) is caught by the final pass.

## System Prompt Management

The server prepends a system prompt to every AI conversation. This prompt is:

- **Configured server-side** — Via the `AI:SystemPrompt` configuration key or a built-in default
- **Injected before sending** — The `SystemPromptService` adds it as the first message in every request
- **Invisible to the client** — Clients cannot see, modify, or override the system prompt
- **Role-enforced** — Client messages are forced to the `user` role, preventing system prompt injection

For play endpoints, the system prompt is dynamically enhanced with the current player state, game context, and available tool descriptions.

## Health Monitoring

The AI provider includes a health check (`AiProviderHealthCheck`) that verifies provider availability with a 10-second timeout. This integrates with ASP.NET Core health check infrastructure for monitoring and load balancer configuration.

## Next Steps

- [API Reference](06-api-reference.md) — Complete endpoint documentation
- [Testing](07-testing.md) — How AI integration is tested with the Fake provider
