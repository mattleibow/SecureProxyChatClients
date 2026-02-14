# Architecture: Secure AI proxy with the Backend-for-Frontend pattern

This article describes the architecture of the **SecureProxyChatClients** reference sample — a .NET 10 solution that demonstrates how to secure AI-powered chat endpoints using the Backend-for-Frontend (BFF) pattern. The sample uses a Blazor WebAssembly client that communicates through a secure ASP.NET Core proxy server to Azure OpenAI.

## Overview

Modern AI-enabled applications face a unique security challenge: the client must interact with an AI service, yet exposing AI credentials, system prompts, or tool-execution capabilities directly to the browser is unacceptable. The BFF pattern solves this by interposing a server-side proxy that owns all secrets and enforces security policy on behalf of the client.

SecureProxyChatClients implements this pattern with 17 defense-in-depth security controls, a pluggable AI provider model based on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions), and .NET Aspire orchestration for local development.

### Key design decisions

| Decision | Rationale |
|---|---|
| Bearer-only API authentication | Prevents CSRF by design — cookies are never sent to API endpoints |
| Server-side system prompt management | Clients cannot inject or override system instructions |
| Tool allowlisting with server-side execution | Prevents arbitrary tool invocation; server tools never leave the trust boundary |
| `IChatClient` on both sides of the proxy | Preserves the MEAI abstraction across the trust boundary so the client programs against the same interface |
| SQLite for conversation persistence | Zero-infrastructure default with EF Core; swap to any provider without code changes |
| .NET Aspire orchestration | Consistent local dev experience with service discovery, health checks, and telemetry |

## Architecture overview

The solution is organized around two trust boundaries: the **untrusted client** running in the browser and the **trusted server** that mediates all access to AI services and persistent state.

### Trust boundaries

```
┌─────────────────────────────────────────────────────────────────┐
│                        UNTRUSTED ZONE                           │
│                                                                 │
│   Blazor WebAssembly Client                                     │
│   ┌───────────────┐  ┌──────────────────┐  ┌────────────────┐  │
│   │ ProxyChatClient│  │ ClientToolRegistry│  │ AuthState      │  │
│   │ (IChatClient)  │  │ (5 client tools)  │  │ (Bearer token) │  │
│   └───────┬───────┘  └──────────────────┘  └────────────────┘  │
│           │ HTTPS + Bearer token                                │
├───────────┼─────────────────────────────────────────────────────┤
│           │         TRUST BOUNDARY                              │
├───────────┼─────────────────────────────────────────────────────┤
│           ▼         TRUSTED ZONE                                │
│   ASP.NET Core Server (BFF Proxy)                               │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │ Security Pipeline                                        │  │
│   │ InputValidator │ ContentFilter │ RateLimiter │ Auth       │  │
│   └──────────────────────────┬───────────────────────────────┘  │
│   ┌──────────────────────────▼───────────────────────────────┐  │
│   │ AI Integration                                           │  │
│   │ SystemPromptService │ ServerToolRegistry │ IChatClient    │  │
│   └──────────────────────────┬───────────────────────────────┘  │
│   ┌──────────────────────────▼───────────────────────────────┐  │
│   │ Data Layer                                               │  │
│   │ EfConversationStore (SQLite) │ VectorStore (pgvector)     │  │
│   └──────────────────────────────────────────────────────────┘  │
│                              │                                  │
├──────────────────────────────┼──────────────────────────────────┤
│                              │  EXTERNAL SERVICES               │
│                              ▼                                  │
│                    Azure OpenAI / AI Provider                    │
└─────────────────────────────────────────────────────────────────┘
```

### Component roles

| Component | Project | Responsibility |
|---|---|---|
| **AppHost** | `SecureProxyChatClients.AppHost` | .NET Aspire orchestration — provisions PostgreSQL (pgvector), wires service references, manages startup order |
| **Server** | `SecureProxyChatClients.Server` | BFF proxy — authentication, input validation, system prompt injection, AI calls, tool execution, output filtering, conversation persistence |
| **Client.Web** | `SecureProxyChatClients.Client.Web` | Blazor WebAssembly SPA — chat UI, client-side tool execution, authentication state management |
| **Shared** | `SecureProxyChatClients.Shared` | DTOs and contracts (`ChatRequest`, `ChatResponse`, `ChatMessageDto`) shared across the trust boundary |
| **ServiceDefaults** | `SecureProxyChatClients.ServiceDefaults` | Aspire service defaults — OpenTelemetry, health checks, service discovery, HTTP resilience |

## Data flow

### Authentication flow

1. The client submits credentials to the ASP.NET Core Identity `/login` endpoint.
2. Identity validates the credentials and checks account lockout status (5 attempts, 5-minute lockout).
3. On success, the server returns a Bearer token.
4. The client stores the token and attaches it to every subsequent API request via `AuthenticatedHttpMessageHandler`.
5. API endpoints require the `IdentityConstants.BearerScheme` explicitly — cookie authentication is scoped to Identity UI endpoints only.

Authentication is configured in [`src/SecureProxyChatClients.Server/Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) (Identity setup, cookie hardening, CORS).

### Chat request flow

1. **Client sends request** — `ProxyChatClient` ([`src/SecureProxyChatClients.Client.Web/Services/ProxyChatClient.cs`](../src/SecureProxyChatClients.Client.Web/Services/ProxyChatClient.cs)) serializes the conversation history and client tool schemas into a `ChatRequest` DTO, then POSTs to `/api/chat` with the Bearer token.

2. **Server validates input** — `InputValidator` ([`src/SecureProxyChatClients.Server/Security/InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs)) enforces all input-side security controls: message count limits, per-message and total length limits, prompt injection detection, role stripping, tool allowlisting, and HTML injection blocking.

3. **Server prepends system prompt** — `SystemPromptService` ([`src/SecureProxyChatClients.Server/Services/SystemPromptService.cs`](../src/SecureProxyChatClients.Server/Services/SystemPromptService.cs)) inserts the server-controlled system prompt at the start of the message list. The client never sees or controls this prompt.

4. **Server calls AI provider** — The request is forwarded to the registered `IChatClient` implementation (Azure OpenAI, Fake, or CopilotCli) via the MEAI abstraction.

5. **Server processes tool calls** — If the AI requests tool execution, the server enters a tool-call loop (see below).

6. **Server filters output** — `ContentFilter` ([`src/SecureProxyChatClients.Server/Security/ContentFilter.cs`](../src/SecureProxyChatClients.Server/Security/ContentFilter.cs)) sanitizes the AI response, removing `<script>`, `<iframe>`, event handlers, and `javascript:` protocol references.

7. **Server persists conversation** — `EfConversationStore` ([`src/SecureProxyChatClients.Server/Data/EfConversationStore.cs`](../src/SecureProxyChatClients.Server/Data/EfConversationStore.cs)) writes the messages to SQLite with per-user session isolation.

8. **Response returned** — The filtered response is serialized and returned to the client.

Chat endpoints are defined in [`src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs`](../src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs).

### Tool execution flow

Tools are split across the trust boundary:

- **Server tools** (GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist) execute entirely on the server. The AI requests them, the server executes them, and the results are fed back into the AI conversation — the client never sees these calls. Server tools are registered in [`src/SecureProxyChatClients.Server/Tools/ServerToolRegistry.cs`](../src/SecureProxyChatClients.Server/Tools/ServerToolRegistry.cs).

- **Client tools** (GetStoryGraph, SearchStory, SaveStoryState, RollDice, GetWorldRules) are returned to the client for local execution. The client runs the tool, then sends the result back to the server in a follow-up request. Client tools are registered in [`src/SecureProxyChatClients.Client.Web/Tools/ClientToolRegistry.cs`](../src/SecureProxyChatClients.Client.Web/Tools/ClientToolRegistry.cs).

The server enforces a maximum tool-call loop depth: **5 rounds** for chat endpoints, **8 rounds** for play endpoints. Tool results are truncated at **32 KB** to prevent token bloat.

## Security model

The solution implements 17 security controls organized in defense-in-depth layers. Each control is identified by a code (S1–S17) used throughout the codebase and test suite.

### Layer 1 — Network and transport

| Control | Description | Implementation |
|---|---|---|
| **S12** | Security headers — CSP, HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy | Inline middleware in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |
| **S13** | CORS with explicit origin — no wildcards, limited methods (GET/POST), specific headers only | CORS policy in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |
| **S15** | Request body size limit — 1 MB maximum enforced at Kestrel level | Kestrel configuration in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |

### Layer 2 — Authentication and authorization

| Control | Description | Implementation |
|---|---|---|
| **S8** | Bearer-only API authentication — API endpoints require `IdentityConstants.BearerScheme`, preventing CSRF | Endpoint group configuration in [`ChatEndpoints.cs`](../src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs) |
| **S9** | Password lockout policy — 5 failed attempts trigger a 5-minute lockout | Identity options in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |
| **S10** | Conversation isolation — `GetSessionOwnerAsync` verifies the authenticated user owns the session before returning data (IDOR prevention) | [`EfConversationStore.cs`](../src/SecureProxyChatClients.Server/Data/EfConversationStore.cs) |
| **S7** | Per-user token bucket rate limiting — partitioned by user ID (authenticated) or IP address (anonymous), configurable permit limit and window | Rate limiter policy in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |

### Layer 3 — Input validation

| Control | Description | Implementation |
|---|---|---|
| **S1** | System prompt injection prevention — strips `system` role messages; strips `assistant`/`tool` roles from the first message position | [`InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs) |
| **S2** | Server-side system prompt management — the system prompt is prepended server-side and never exposed to or controllable by the client | [`SystemPromptService.cs`](../src/SecureProxyChatClients.Server/Services/SystemPromptService.cs) |
| **S3** | Prompt injection detection — 11 blocked patterns (e.g., "ignore previous instructions", "system prompt:", "override instructions") | [`InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs), patterns defined in [`SecurityOptions.cs`](../src/SecureProxyChatClients.Server/Security/SecurityOptions.cs) |
| **S4** | Input length limits — max 10 messages, 4,000 chars per message, 50,000 chars total (all configurable) | [`InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs) |
| **S5** | Tool schema validation — client tool names must appear in a server-side allowlist | [`InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs), allowlist in [`SecurityOptions.cs`](../src/SecureProxyChatClients.Server/Security/SecurityOptions.cs) |
| **S6** | HTML/script injection filtering on input — blocks `<script>`, `<iframe>`, `javascript:`, `onerror=`, `onload=` | [`InputValidator.cs`](../src/SecureProxyChatClients.Server/Security/InputValidator.cs) |

### Layer 4 — Output filtering

| Control | Description | Implementation |
|---|---|---|
| **S6** | HTML/script injection filtering on output — regex-based removal of script tags, iframe tags, event handlers, and javascript: protocol from AI responses | [`ContentFilter.cs`](../src/SecureProxyChatClients.Server/Security/ContentFilter.cs) |
| **S16** | Tool result size limit — 32 KB maximum per tool result to prevent token bloat | Constants in [`ChatEndpoints.cs`](../src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs) and [`PlayEndpoints.cs`](../src/SecureProxyChatClients.Server/Endpoints/PlayEndpoints.cs) |

### Layer 5 — Error handling and audit

| Control | Description | Implementation |
|---|---|---|
| **S14** | Global exception handler — returns RFC 9457 `ProblemDetails` responses; never exposes stack traces or internal details to clients | [`GlobalExceptionHandler.cs`](../src/SecureProxyChatClients.Server/Security/GlobalExceptionHandler.cs) |
| **S11** | Security audit logging — logs all 401/403 responses with user identity, path, and status code | Audit middleware in [`Program.cs`](../src/SecureProxyChatClients.Server/Program.cs) |
| **S17** | AI call timeout — 5-minute cancellation token for streaming requests to prevent hung connections | Streaming endpoint in [`ChatEndpoints.cs`](../src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs) |

Security options are centralized in [`SecurityOptions.cs`](../src/SecureProxyChatClients.Server/Security/SecurityOptions.cs) and bound from the `Security` configuration section.

## AI integration patterns

### Provider abstraction

The solution uses the [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions) (`IChatClient`) abstraction to decouple AI provider selection from application logic. Three providers are supported:

| Provider | Use case | Configuration value |
|---|---|---|
| **AzureOpenAI** | Production — connects to Azure OpenAI via `AzureOpenAIClient` | `AI:Provider = "AzureOpenAI"` |
| **Fake** | Unit/integration testing — returns deterministic responses | `AI:Provider = "Fake"` (default) |
| **CopilotCli** | Local development with GitHub Copilot | `AI:Provider = "CopilotCli"` |

Provider registration is in [`src/SecureProxyChatClients.Server/AI/AiServiceExtensions.cs`](../src/SecureProxyChatClients.Server/AI/AiServiceExtensions.cs). Every provider is wrapped with `ObservabilityChatClient` for telemetry.

### Client-side IChatClient

The Blazor client also programs against `IChatClient` via `ProxyChatClient` ([`src/SecureProxyChatClients.Client.Web/Services/ProxyChatClient.cs`](../src/SecureProxyChatClients.Client.Web/Services/ProxyChatClient.cs)). This class serializes MEAI types into shared DTOs, sends them to the server, and handles client-side tool execution transparently. The same `IChatClient` interface is preserved on both sides of the trust boundary.

### Tool calling

The server implements a multi-round tool execution loop:

1. The AI response is inspected for `FunctionCallContent` items.
2. Each tool call is classified as a **server tool** or a **client tool**.
3. Server tools are executed immediately and their results appended to the conversation.
4. Client tools are returned to the client in the response for local execution.
5. The loop repeats until no more tool calls are requested or the round limit is reached.

Server tools are implemented as `AIFunction` instances in [`src/SecureProxyChatClients.Server/Tools/`](../src/SecureProxyChatClients.Server/Tools/).

### Streaming

The streaming endpoint uses Server-Sent Events (SSE). The server calls `IChatClient.GetStreamingResponseAsync()` and forwards each `ChatResponseUpdate` as a `text-delta` event. A `done` event with the session ID is sent on completion. The stream is protected by a 5-minute cancellation timeout.

### Structured output

Play endpoints use `ChatResponseFormat.ForJsonSchema` to request structured JSON output from the AI, enabling typed game-state responses.

## State management

### Conversation persistence

Conversations are stored via the `IConversationStore` abstraction ([`src/SecureProxyChatClients.Server/Data/IConversationStore.cs`](../src/SecureProxyChatClients.Server/Data/IConversationStore.cs)), implemented by `EfConversationStore` with SQLite.

The store manages two entities:

- **ConversationSession** — ID, user ID, title (auto-generated from first user message), timestamps.
- **ConversationMessage** — ID, session ID, role, content, author name, sequence number, timestamp.

Sessions are scoped to the authenticated user. The `GetSessionOwnerAsync` method supports the IDOR prevention check (S10).

### Game state

Game state (player data, achievements, bestiary progress) is managed by `IGameStateStore` with an in-memory implementation. Game engine components are in [`src/SecureProxyChatClients.Server/GameEngine/`](../src/SecureProxyChatClients.Server/GameEngine/).

### Vector store

Story memory search uses a vector store abstraction (`IStoryMemoryService`) with two implementations:

- **PgVectorStoryMemoryService** — PostgreSQL with pgvector for production semantic search.
- **InMemoryStoryMemoryService** — Fallback when no PostgreSQL connection string is configured.

Vector store components are in [`src/SecureProxyChatClients.Server/VectorStore/`](../src/SecureProxyChatClients.Server/VectorStore/).

## Observability and health

### OpenTelemetry

The ServiceDefaults project ([`src/SecureProxyChatClients.ServiceDefaults/Extensions.cs`](../src/SecureProxyChatClients.ServiceDefaults/Extensions.cs)) configures OpenTelemetry with:

- **Metrics** — ASP.NET Core, HTTP client, .NET runtime instrumentation, plus a custom `SecureProxyChatClients.AI` meter.
- **Traces** — ASP.NET Core and HTTP client instrumentation, with health check endpoints excluded from trace collection.
- **Logs** — OpenTelemetry log exporter with formatted messages and scopes.
- **Export** — OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured; Azure Monitor integration available.

### Custom AI metrics

`ObservabilityChatClient` ([`src/SecureProxyChatClients.Server/AI/ObservabilityChatClient.cs`](../src/SecureProxyChatClients.Server/AI/ObservabilityChatClient.cs)) is a `DelegatingChatClient` that emits five custom metrics:

| Metric | Type | Description |
|---|---|---|
| `ai.prompt_tokens` | Counter | Prompt tokens sent to the AI provider |
| `ai.completion_tokens` | Counter | Completion tokens received |
| `ai.request_duration` | Histogram | AI request latency in milliseconds |
| `ai.errors` | Counter | Failed AI requests |
| `ai.requests` | Counter | Total AI requests |

### Health checks

Two health check endpoints are exposed in development:

| Endpoint | Scope | Tags |
|---|---|---|
| `/health` | All registered checks (liveness + readiness) | — |
| `/alive` | Liveness only — confirms the process is responsive | `live` |

The `AiProviderHealthCheck` ([`src/SecureProxyChatClients.Server/AI/AiProviderHealthCheck.cs`](../src/SecureProxyChatClients.Server/AI/AiProviderHealthCheck.cs)) sends a minimal "ping" request to the AI provider with a 10-second timeout and reports Healthy, Degraded, or Unhealthy status. It is tagged `ready` for readiness probes.

## Configuration

Configuration is loaded in this precedence order (highest wins):

1. Environment variables
2. `secrets.json` (gitignored, loaded from solution root)
3. `appsettings.{Environment}.json`
4. `appsettings.json`

### Key configuration sections

| Section | Keys | Default | Purpose |
|---|---|---|---|
| `AI` | `Provider`, `Endpoint`, `ApiKey`, `DeploymentName`, `SystemPrompt` | `Fake` provider, `gpt-4o` deployment | AI provider selection and credentials |
| `AI:CopilotCli` | `Model` | `gpt-5-mini` | Model override for CopilotCli provider |
| `Client` | `Origin` | `https://localhost:5002` | Allowed CORS origin |
| `RateLimiting` | `PermitLimit`, `WindowSeconds` | 30 requests / 60 seconds | Token bucket rate limiter tuning |
| `Security` | `MaxMessages`, `MaxMessageLength`, `MaxTotalLength`, `AllowedToolNames`, `BlockedPatterns` | 10 messages, 4000 chars, 50000 total | Input validation thresholds |
| `ConnectionStrings` | `DefaultConnection`, `VectorStore` | `Data Source=app.db` | SQLite (conversations) and PostgreSQL (vectors) |
| `SeedUser` | `Email`, `Password` | `test@test.com` / `Test123!` | Development seed user |

## Project structure

```
SecureProxyChatClients/
├── src/
│   ├── SecureProxyChatClients.AppHost/          # .NET Aspire orchestration
│   │   └── AppHost.cs                           # PostgreSQL + server + client wiring
│   ├── SecureProxyChatClients.Client.Web/        # Blazor WebAssembly client
│   │   ├── Agents/                              # Multi-agent collaboration (WritersRoom)
│   │   ├── Components/                          # Shared Razor components
│   │   ├── Pages/                               # Razor pages (Chat, Play, Login, etc.)
│   │   ├── Services/                            # ProxyChatClient, AuthState
│   │   └── Tools/                               # Client-side tool implementations
│   ├── SecureProxyChatClients.Server/            # ASP.NET Core API (BFF proxy)
│   │   ├── AI/                                  # Provider abstraction, health check, observability
│   │   ├── Data/                                # EF Core entities, conversation store
│   │   ├── Endpoints/                           # Minimal API endpoint groups
│   │   ├── GameEngine/                          # Game state, achievements, bestiary
│   │   ├── Security/                            # InputValidator, ContentFilter, GlobalExceptionHandler
│   │   ├── Services/                            # SystemPromptService, SeedDataService
│   │   ├── Tools/                               # Server-side AI tool implementations
│   │   └── VectorStore/                         # Story memory (pgvector / in-memory)
│   ├── SecureProxyChatClients.ServiceDefaults/   # Aspire service defaults
│   │   └── Extensions.cs                        # OpenTelemetry, health checks, resilience
│   └── SecureProxyChatClients.Shared/            # Shared contracts
│       └── Contracts/                           # DTOs for cross-boundary communication
├── tests/
│   ├── SecureProxyChatClients.Tests.Unit/        # xUnit unit tests (~258 tests)
│   ├── SecureProxyChatClients.Tests.Integration/ # Aspire integration tests
│   ├── SecureProxyChatClients.Tests.Playwright/  # End-to-end browser tests
│   └── SecureProxyChatClients.Tests.Smoke/       # Smoke tests (real AI provider)
├── docs/                                        # Documentation
│   ├── api.md                                   # REST API reference
│   └── lore-engine.md                           # Game design document
├── Directory.Build.props                        # Shared MSBuild properties
└── SecureProxyChatClients.slnx                  # Solution file
```

## Related resources

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/dotnet/ai/ai-extensions)
- [.NET Aspire overview](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview)
- [Backend for Frontends pattern](https://learn.microsoft.com/azure/architecture/patterns/backends-for-frontends)
- [ASP.NET Core Identity with Bearer tokens](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
- [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
- [OpenTelemetry in .NET](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Blazor WebAssembly overview](https://learn.microsoft.com/aspnet/core/blazor/)
- [REST API reference for this sample](api.md)
- [LoreEngine game design](lore-engine.md)
- [Security threat model](ag-ui-security-considerations.md)
