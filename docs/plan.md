# Reference Sample: SecureProxyChatClients

> **Created**: 2026-02-13
> **Status**: Reviewed — ready for Phase 1 implementation

## Problem Statement

MAUI (and any client) apps cannot safely embed AI provider credentials or trust client-provided messages. We need a reference implementation of a **secure augmenting proxy** (BFF pattern) that mediates between untrusted clients and Azure OpenAI. The server doesn't just forward requests — it authenticates users, enforces security policies, executes server-side tools, filters content, and enriches/augments client requests before forwarding to AI. The MEAI `IChatClient` abstraction is preserved on both sides of the trust boundary.

### Terminology

| Term | Meaning |
|------|---------|
| **Server** / **Secure Augmenting Proxy** / **BFF** | The ASP.NET Core web app that mediates between clients and Azure OpenAI |
| **Client** / **WASM App** | The standalone Blazor WebAssembly app — untrusted, runs agents locally |
| **ProxyChatClient** | Client-side `IChatClient` implementation that calls the server's API with bearer token auth |
| **Agent** | A `ChatClientAgent` instance running in WASM (NOT on the server) — only exists from Phase 9+ |

## Requirements

### Core Infrastructure Requirements

| # | Requirement | Description |
|---|-------------|-------------|
| R1 | **Secure proxy** | Server owns all AI provider credentials; client never sees them |
| R2 | **IChatClient on both sides** | `ProxyChatClient` on client implements `IChatClient`; server uses real Azure OpenAI `IChatClient` |
| R3 | **Streaming (SSE)** | Server streams AI responses token-by-token via SSE-formatted stream over HttpClient; client consumes as `IAsyncEnumerable<ChatResponseUpdate>` |
| R4 | **Dual authentication** | ASP.NET local auth (dev + v1); Microsoft Entra ID (Phase 12+ additive) |
| R5 | **Server→OpenAI auth** | Entra ID managed identity preferred; PAT via env var fallback |
| R6 | **Server-side tools** | AIFunctions registered and executed on the server; AI model requests them, server executes and returns result via IChatClient |
| R7 | **Client-side tools** | AIFunctions registered and executed locally in client (Blazor WASM); AI model requests them, client executes and feeds result back into conversation |
| R8 | **Conversation persistence** | Server persists conversation history for audit/resume (`IConversationStore`); client is authoritative for context window (builds `messages` payload). Client owns story/game state locally (IndexedDB). |
| R9 | **Structured output** | Typed response schemas via `ChatOptions.ResponseFormat` |
| R10 | **Multi-agent orchestration** | Agent Framework `GroupChatOrchestrator` on the CLIENT (Blazor WASM) with `ChatClientAgent` instances calling server via `ProxyChatClient` |
| R11 | **Aspire orchestration** | Single F5 launches everything — AppHost orchestrates server + client |

### Security Requirements (from AG-UI Security Doc)

| # | Requirement | Description |
|---|-------------|-------------|
| S1 | **Role stripping** | Force all user-authored prompt messages to `role: user` — prevent system message injection. Does NOT strip `assistant`/`tool` roles from tool continuation messages. |
| S2 | **Input validation** | Message length limits, content sanitization, format validation |
| S3 | **Rate limiting** | Per-user rate limiting via `Microsoft.AspNetCore.RateLimiting` |
| S4 | **Content filtering** | Sanitize LLM output before sending to client (XSS prevention) |
| S5 | **Tool allowlisting** | Server maintains allowlist of permitted tool names; rejects client-provided tool schemas not on the list; validates schema structure before forwarding to AI |
| S6 | **Sensitive data filtering** | Strip API keys, PII, stack traces from tool results and responses |
| S7 | **Session security** | Server-generated session IDs, ownership verification, no arbitrary thread access |
| S8 | **Client tool result validation** | Type check, size limit, injection detection on all client-provided tool results |

### Technology Stack

| Component | Version/Package | Notes |
|-----------|----------------|-------|
| .NET | 10.0 (LTS) | TFM `net10.0` |
| C# | 14 | Extension members, field keyword, null-conditional assignment |
| Aspire | Latest stable | `Aspire.Hosting.*` |
| MEAI | Latest stable | `Microsoft.Extensions.AI` + `.OpenAI` |
| Agent Framework | Latest | `Microsoft.Agents.AI` (ChatClientAgent, GroupChatOrchestrator) |
| ASP.NET Core | 10.0 | Minimal APIs |
| Auth | ASP.NET Core Identity API endpoints (v1); `Microsoft.Identity.Web` (Phase 12+ Entra ID) | Local auth for v1 |
| Azure OpenAI | `Azure.AI.OpenAI` | AI provider |

### Showcase App: LoreEngine (Simplified)

The infrastructure is demonstrated through **LoreEngine**, a simplified interactive fiction builder. See **`docs/lore-engine.md`** for the full game design.

**v1 scope**: 3 agents (Storyteller, Critic, Archivist) in Creation Mode only. 4 server tools + 5 client tools.

**Key architectural choice**: Agents live on the **CLIENT** (Blazor WASM). Each agent's `ChatClientAgent` calls `ProxyChatClient` → server → Azure OpenAI. The server is a **secure augmenting proxy** — it adds auth, security, server-side tools, content filtering, and can enrich/augment client requests before forwarding to AI. It has no game logic. See `docs/lore-engine.md` for agent details, tool tables, and example sessions.

### Priority: Infrastructure First, Game Second

We do NOT spend hours on game polish. The reference sample (secure proxy architecture) is the deliverable; the game is the demo.

## Solution Structure

```
SecureProxyChatClients/
├── README.md                                ← Setup instructions, architecture diagram
├── .github/
│   └── copilot-instructions.md          ← AI assistant rules & learnings
├── memory-bank/                          ← Persistent context for AI assistants
│   ├── productContext.md
│   ├── activeContext.md
│   ├── systemPatterns.md
│   ├── decisionLog.md
│   └── progress.md
├── docs/
│   ├── proposal.md                       ← Original proposal
│   ├── research.md                       ← Deep research output
│   ├── ag-ui-security-considerations.md  ← Downloaded security doc
│   ├── lore-engine.md                    ← Game concept & design
│   └── plan.md                           ← THIS FILE (reference sample requirements)
├── src/
│   ├── SecureProxyChatClients.AppHost/           ← Aspire orchestrator
│   │   └── Program.cs
│   ├── SecureProxyChatClients.ServiceDefaults/   ← Shared Aspire defaults
│   │   └── Extensions.cs
│   ├── SecureProxyChatClients.Server/            ← ASP.NET Core web app with Identity UI + secure augmenting proxy API
│   │   ├── Program.cs
│   │   ├── Data/
│   │   │   └── AppDbContext.cs                    ← Identity + app data (SQLite)
│   │   ├── Pages/                                 ← Identity UI (registration, account management — Razor Pages from template)
│   │   ├── Endpoints/
│   │   │   └── ChatEndpoints.cs                   ← POST /api/chat, POST /api/chat/stream
│   │   ├── Middleware/
│   │   │   ├── RoleStrippingMiddleware.cs
│   │   │   ├── InputValidationMiddleware.cs
│   │   │   └── ContentFilteringMiddleware.cs
│   │   ├── Services/
│   │   │   ├── ChatProxyService.cs                ← Core proxy logic
│   │   │   ├── ConversationStore.cs               ← IConversationStore (in-memory, pluggable)
│   │   │   └── CopilotCliChatClient.cs            ← Dev-time IChatClient via copilot CLI
│   │   ├── Tools/                                 ← Server-side AIFunctions (GenerateScene, etc.)
│   │   └── Auth/
│   │       ├── PatAuthenticationHandler.cs
│   │       └── DualAuthConfiguration.cs
│   ├── SecureProxyChatClients.Client.Web/         ← Blazor WASM standalone (separate app — login only, agents + game logic)
│   │   ├── Program.cs
│   │   ├── wwwroot/
│   │   ├── Layout/
│   │   │   └── MainLayout.razor
│   │   ├── Pages/
│   │   │   ├── Chat.razor                         ← Main chat page (Creation Mode)
│   │   │   └── Login.razor                        ← Login page (calls server /login API, no registration)
│   │   ├── Components/
│   │   │   ├── ChatMessage.razor                  ← Message bubble component
│   │   │   ├── StreamingText.razor                ← Token-by-token render
│   │   │   └── AgentBadge.razor                   ← Agent attribution label
│   │   ├── Services/
│   │   │   ├── ProxyChatClient.cs                 ← IChatClient over HTTP/SSE
│   │   │   ├── AuthService.cs                     ← Token management
│   │   │   └── StoryStateService.cs               ← IndexedDB story persistence
│   │   ├── Agents/                                ← Agent orchestration (Storyteller, Critic, Archivist)
│   │   │   ├── WritersRoom.cs                     ← GroupChatOrchestrator setup
│   │   │   └── AgentDefinitions.cs                ← System prompts + agent config
│   │   └── Tools/                                 ← Client-side AIFunctions (GetStoryGraph, etc.)
│   └── SecureProxyChatClients.Shared/            ← Shared models & contracts
│       ├── Models/
│       │   ├── ChatRequest.cs
│       │   ├── ChatStreamEvent.cs
│       │   ├── ToolCallRequest.cs
│       │   ├── ToolCallResult.cs
│       │   └── SessionInfo.cs
│       └── Contracts/
│           └── IChatApi.cs
├── tests/
│   ├── SecureProxyChatClients.Tests.Unit/         ← Fast unit tests (FakeChatClient)
│   │   ├── Middleware/
│   │   ├── Services/
│   │   ├── Tools/
│   │   └── FakeChatClient.cs                      ← Deterministic AI mock
│   ├── SecureProxyChatClients.Tests.Integration/  ← Aspire integration tests (HTTP-level)
│   │   ├── ChatEndpointTests.cs
│   │   ├── StreamingTests.cs
│   │   ├── AuthTests.cs
│   │   ├── ToolCallTests.cs
│   │   ├── SessionTests.cs
│   │   └── SecurityTests.cs
│   ├── SecureProxyChatClients.Tests.Playwright/   ← Playwright E2E (browser automation)
│   │   ├── ChatTests.cs                           ← Type message, see response
│   │   ├── StreamingTests.cs                      ← Verify tokens appear progressively
│   │   ├── AuthTests.cs                           ← Login flow, 401 redirect
│   │   ├── ToolCallTests.cs                       ← Tool call prompt in UI
│   │   ├── AgentTests.cs                          ← Writer's Room agent labels
│   │   └── CreationModeTests.cs                   ← Creation Mode flow (pitch → debate → draft)
│   └── SecureProxyChatClients.Tests.Smoke/        ← Real OpenAI E2E validation
│       ├── ConversationSmokeTests.cs
│       ├── ToolCallSmokeTests.cs
│       └── StreamingSmokeTests.cs
└── SecureProxyChatClients.sln
```

## Testing Strategy

### Four Test Layers

```
┌──────────────────────────────────────────────────────────────────┐
│  Layer 4: Smoke Tests (Real OpenAI)                              │
│  - Full Aspire app + real Azure OpenAI                           │
│  - Validates actual AI responses, streaming, tool calling        │
│  - Run on-demand; burns tokens                                   │
├──────────────────────────────────────────────────────────────────┤
│  Layer 3: Playwright E2E (Browser Automation)                    │
│  - Starts full Aspire app (server + Blazor WASM)                 │
│  - Drives the UI: type messages, click buttons, verify DOM       │
│  - Tests streaming render, tool call prompts, auth flows, agents │
│  - Uses FakeChatClient for determinism OR real OpenAI for smoke  │
│  - Copilot runs these after each phase to validate work          │
├──────────────────────────────────────────────────────────────────┤
│  Layer 2: Integration Tests (FakeChatClient + Aspire HTTP)       │
│  - DistributedApplicationTestingBuilder spins up full app        │
│  - Tests HTTP endpoints directly (no browser)                    │
│  - SSE streaming, auth headers, tool call protocol               │
│  - Fast, deterministic, CI-safe                                  │
├──────────────────────────────────────────────────────────────────┤
│  Layer 1: Unit Tests (FakeChatClient, no server)                 │
│  - Middleware logic, tool validation, session management          │
│  - ProxyChatClient serialization/deserialization                 │
│  - Fastest layer, pure logic tests                               │
└──────────────────────────────────────────────────────────────────┘
```

### Three IChatClient Implementations

| Implementation | Use Case | AI Provider | Streaming | Tool Calls |
|---------------|----------|-------------|-----------|------------|
| `FakeChatClient` | Unit tests, CI | None (queued responses) | Simulated | Simulated |
| `CopilotCliChatClient` | Dev/manual testing | GitHub Copilot (gpt-5-mini) | Simulated | JSON simulation |
| Azure OpenAI `IChatClient` | Production + smoke tests | Azure OpenAI | Real SSE | Real |

Config-based switching via `AI:Provider` in appsettings: `fake` / `copilot-cli` / `azure-openai`.

See `docs/recommendations.md` for implementation code.

### Playwright E2E Tests

Playwright tests drive the Blazor WASM UI in a real browser. The app is started via Aspire, then Playwright navigates to it. Registration happens on the server app; login + chat on the WASM app.

See `docs/recommendations.md` for test code examples.

**Key Playwright patterns:**
- `data-testid` attributes on all interactive elements
- `Expect(...).ToContainTextAsync(...)` with timeouts for streaming
- Registration on server app, login on WASM app
- Progressive content checks for streaming validation

### Integration Tests (Aspire HTTP-level)

`DistributedApplicationTestingBuilder` + `HttpClient` — tests the API directly without a browser. See `docs/recommendations.md` for code.

### Per-Phase Validation (Copilot Workflow)

After completing each phase, Copilot will:

1. **Build**: `dotnet build` — verify compilation
2. **Unit tests**: `dotnet test tests/SecureProxyChatClients.Tests.Unit` — verify logic
3. **Integration tests**: `dotnet test tests/SecureProxyChatClients.Tests.Integration` — verify API endpoints
4. **Playwright tests**: `dotnet test tests/SecureProxyChatClients.Tests.Playwright` — verify UI behavior in browser
5. **Smoke test** (if credentials available): `dotnet test --filter Category=Smoke`
6. **Update memory-bank**: Record results and learnings

### Test Coverage per Phase

| Phase | Unit Tests | Integration Tests | Playwright E2E | Smoke Tests |
|-------|-----------|------------------|----------------|-------------|
| 1. Foundation + Auth | — | 401 without auth, 200 with token | Register (server), login (WASM), call protected API | — |
| 2. Basic Chat | ProxyChatClient serialization | Chat endpoint returns response | Type message → see response | Full conversation |
| 3. Streaming | — | SSE event parsing | Tokens appear progressively | Real streaming |
| 4. Security | Role stripping, input validation | System messages rejected, rate limit | — (covered by integration) | Injection blocked |
| 5. Server Tools | Tool execution, result filtering | Tool call triggered via API | Tool result appears in chat | AI calls real tool |
| 6. Client Tools | Tool result validation | Full round-trip via HTTP | Tool call prompt in UI, user interaction | Multi-turn flow |
| 7. Persistence | Session create/get/validate | Session persists, ownership | Refresh page → conversation persists | Resume conversation |
| 8. Structured Output | Schema deserialization | Typed response returned | Structured data renders in UI | AI returns schema |
| 9. Multi-Agent | — | Agent messages in SSE | Agent badges visible in UI | Writer's Room |
| 10. Game Layer | — | Story persistence endpoints | Creation Mode flow: pitch → debate → draft | Full creation cycle |

### Test Project Dependencies

```
Tests.Unit
  └── references → Shared, Server, Client.Web (for ProxyChatClient)

Tests.Integration
  └── references → AppHost (via Aspire.Hosting.Testing)

Tests.Playwright
  └── references → AppHost (via Aspire.Hosting.Testing)
  └── packages  → Microsoft.Playwright.Xunit

Tests.Smoke
  └── references → AppHost (via Aspire.Hosting.Testing)
  └── requires  → AZURE_OPENAI_ENDPOINT + credentials in environment
```

### NuGet Packages (Testing)

- `xunit` + `xunit.runner.visualstudio` — All test projects (standardized on xUnit)
- `Aspire.Hosting.Testing` — Aspire app testing
- `Microsoft.Playwright.Xunit` — Playwright browser automation
- `FluentAssertions` — Readable assertions (optional)

## Phase Breakdown

> **Principle**: Each phase adds INFRASTRUCTURE first, then a thin game feature to demonstrate it (see `docs/lore-engine.md`). Infrastructure is the deliverable; the game is the demo.

### Phase 1: Foundation + Auth (Skeleton with Identity)

**Infrastructure**: Aspire orchestration, ASP.NET Core web app with Identity (registration UI + API), separate plain Blazor WASM client, authenticated cross-origin comms.
**Demo**: Register on the server web app, log in from the WASM app, call a protected API endpoint.

| Task | Description |
|------|-------------|
| **1.1** Create solution structure | `dotnet new` for all projects (AppHost, ServiceDefaults, Server with `--auth Individual`, Client.Web as plain WASM, Shared, test projects), net10.0 TFM, C# 14 |
| **1.2** Server with Identity | ASP.NET Core web app using `.AddIdentityApiEndpoints()` + SQLite — registration UI (Razor Pages from template) + REST API endpoints (`/login`). Bearer token auth. CORS configured for WASM origin. |
| **1.3** Seed test user | Seed a known test user at startup (for dev + Playwright); configurable via appsettings |
| **1.4** Blazor WASM login | Plain standalone WASM app with a Login.razor page that calls server's `/login` API to get a bearer token. No registration — user registers on server web UI. |
| **1.5** Authenticated HttpClient | `ProxyChatClient` (stub) attaches `Authorization: Bearer <token>` to every cross-origin request to the server |
| **1.6** Protected endpoint | `GET /api/ping` — returns 200 with user info if authenticated, 401 if not. Proves cross-origin auth works. |
| **1.7** Aspire wiring | AppHost orchestrates server + WASM as separate apps on separate ports. CORS policy configured. |
| **1.8** Shared contracts | `ChatRequest`, `ChatResponse`, `ChatStreamEvent` records (ready for Phase 2) |
| **1.9** Test infrastructure | Test projects created. First Playwright test: navigate to server → register → navigate to WASM app → log in → call protected endpoint → see result. |

**Exit Criteria**: F5 → both apps launch → register on server → log in from WASM → call authenticated API → see response. Playwright tests pass for registration (server) + login (WASM) + 401 redirect.

### Phase 2: Basic Chat (Proxy + AI)

**Infrastructure**: IChatClient proxy, basic HTTP chat endpoint, CopilotCliChatClient.
**Demo**: Authenticated user types message, gets AI response through the secure proxy.

| Task | Description |
|------|-------------|
| **2.1** Server chat endpoint | `POST /api/chat` (requires auth) → forwards to `IChatClient` |
| **2.2** CopilotCliChatClient | `IChatClient` over `copilot -p` CLI — real AI with zero Azure setup (dev only, default gpt-5-mini) |
| **2.3** AI provider switcher | Config-based `IChatClient` registration: `copilot-cli` / `azure-openai` / `fake` via `appsettings` |
| **2.4** FakeChatClient | Deterministic `IChatClient` for unit/integration tests (queued responses) |
| **2.5** ProxyChatClient | Client-side `IChatClient` that calls `POST /api/chat` with bearer token (in Blazor WASM) |
| **2.6** Blazor chat page | Chat.razor — text input, send button, response display (behind auth) |
| **2.7** State store abstraction | `IConversationStore` interface, in-memory implementation (marked non-production) |

**Exit Criteria**: F5 → log in → type message → get real AI response through authenticated proxy (via CopilotCliChatClient). Playwright test passes.

### Phase 3: Streaming

**Infrastructure**: SSE streaming endpoint, `IAsyncEnumerable` consumption.
**Demo**: AI responses stream token-by-token through authenticated proxy.

| Task | Description |
|------|-------------|
| **3.1** SSE streaming endpoint | `POST /api/chat/stream` (requires auth) returns `text/event-stream` |
| **3.2** Server streaming | `GetStreamingResponseAsync` → yield `ChatStreamEvent` as SSE with event IDs + heartbeats |
| **3.3** ProxyChatClient streaming | `GetStreamingResponseAsync` consumes SSE as `IAsyncEnumerable<ChatResponseUpdate>` via HttpClient streaming |
| **3.4** Blazor streaming render | StreamingText.razor component — tokens appear progressively |

**Exit Criteria**: Tokens appear one-by-one in browser as AI generates them. Playwright verifies progressive content growth.

### Phase 4: Security Hardening

**Infrastructure**: Role stripping, input validation, rate limiting, content filtering.
**Demo**: Injected system messages are rejected.

| Task | Description |
|------|-------------|
| **4.1** Role stripping middleware | Force user-authored prompt messages to `role: user`; allow `assistant`/`tool` roles in tool continuation flows |
| **4.2** Input validation | Message length limits, content sanitization |
| **4.3** Rate limiting | Per-user rate limiting via `Microsoft.AspNetCore.RateLimiting` |
| **4.4** Content filtering | Sanitize LLM output (strip potential XSS, redact sensitive data) |
| **4.5** Tool allowlisting | Infrastructure for tool allowlist (config-based); tested fully in Phase 5 when tools are registered |
| **4.6** Sensitive data filtering | Strip keys, PII, stack traces from responses |

**Exit Criteria**: System message injection blocked; rate limits enforced; output sanitized.

### Phase 5: Server-Side Tools

**Infrastructure**: AIFunction registration and execution on the server.
**Demo**: AI invokes server-hosted tools and returns results. (LoreEngine: GenerateScene, CreateCharacter)

| Task | Description |
|------|-------------|
| **5.1** Tool registration | Register server tools as `AIFunction` in DI |
| **5.2** Tool execution flow | AI requests tool call → server executes → result in conversation |
| **5.3** Tool result filtering | Strip sensitive data from tool results |

**Exit Criteria**: AI triggers server-side tool, executes, and returns structured result.

### Phase 6: Client-Side Tools

**Infrastructure**: Client-local AIFunction execution in Blazor WASM.
**Demo**: AI model requests tools that execute on the client against local data. (LoreEngine: GetStoryGraph, SaveStoryState)

| Task | Description |
|------|-------------|
| **6.1** Client tool registration | Register client-local tools as AIFunctions in WASM DI |
| **6.2** IndexedDB story storage | `StoryStateService` for persisting story graph to browser storage (basic get/put for tool results) |
| **6.3** Tool execution loop | AI model requests client tool → ProxyChatClient executes locally → result fed back into conversation |
| **6.4** End-to-end flow | AI model calls client tool (GetStoryGraph) → uses result → calls server tool (GenerateScene) → combined result |
| **6.5** Client tool result validation | Server validates client-provided tool results: type check, size limit, injection detection (implements S8) |

**Exit Criteria**: AI model calls client-side tools via ProxyChatClient loop, reads/writes local story data, combines with server tools. Tool results validated server-side.

### Phase 7: Conversation Persistence & Sessions

**Infrastructure**: Server-side conversation history persistence for audit/resume; client-owned story state.
**Demo**: Conversation survives across requests; story persists in browser across refresh.

| Task | Description |
|------|-------------|
| **7.1** Server conversation persistence | `IConversationStore` persists conversation history (for audit, resume, cross-device). Server stores but client remains authoritative for context window. |
| **7.2** Client story state persistence | IndexedDB — story state survives browser refresh (builds on Phase 6.2 StoryStateService) |
| **7.3** Session security | Server-generated session IDs, ownership verification, no arbitrary thread access |

**Exit Criteria**: Conversation history saved server-side and retrievable. Story state survives browser refresh via IndexedDB. Session ownership prevents cross-user access. Playwright test: refresh page → re-login → conversation history still visible (bearer token lost on refresh requires re-login; conversation reloaded from server).

### Phase 8: Structured Output

**Infrastructure**: Typed response schemas via `ChatOptions.ResponseFormat`.
**Demo**: AI returns typed objects. (LoreEngine: Scene, Character, StoryAnalysis schemas)

| Task | Description |
|------|-------------|
| **8.1** Define schemas | Typed record definitions for structured responses |
| **8.2** Server schema enforcement | Pass schema to `ChatOptions.ResponseFormat` |
| **8.3** Client typed deserialization | Deserialize structured responses into typed objects |

**Exit Criteria**: AI returns typed objects matching defined schemas.

### Phase 9: Multi-Agent Orchestration

**Infrastructure**: Agent Framework `GroupChatOrchestrator` on the **CLIENT (Blazor WASM)**.
**Demo**: Multiple agents collaborate on a response. (LoreEngine: Writer's Room)

| Task | Description |
|------|-------------|
| **9.1** Agent definitions | 3 `ChatClientAgent` instances (Storyteller, Critic, Archivist) with distinct system prompts, in Blazor WASM |
| **9.2** Group Chat orchestrator | `GroupChatOrchestrator` with custom `WritersRoomStrategy` — runs in client WASM |
| **9.3** Agent→ProxyChatClient | Each agent calls `ProxyChatClient` → server → Azure OpenAI for completions |
| **9.4** Agent tool binding | Agents use both server tools (via ProxyChatClient) and client tools (local) |
| **9.5** Blazor agent rendering | AgentBadge.razor — show agent name + emoji on each message |

**Exit Criteria**: User pitches an idea → 3 agents discuss in group chat → agent badges visible in browser. Playwright verifies.

### Phase 10: Game Polish (LoreEngine Creation Mode)

**Infrastructure**: Story state persistence, scene-scoped context.
**Demo**: Full LoreEngine Creation Mode — pitch → debate → draft cycle.

| Task | Description |
|------|-------------|
| **10.1** Story state persistence | IndexedDB storage for story graph, characters, world rules (extends Phase 6 StoryStateService) |
| **10.2** Scene-scoped context | Client builds scoped context (current scene + neighbors) for server requests |
| **10.3** Creation flow | Pitch → Writer's Room debate → user decision → draft generation |

**Exit Criteria**: Full creation cycle works. Story persists across browser sessions. Playwright tests the pitch→debate→draft flow. (Note: Play Mode and Export are deferred to Phase 12+ — see `docs/lore-engine.md`.)

### Phase 11: Documentation & Polish

| Task | Description |
|------|-------------|
| **11.1** README.md | Setup instructions, architecture diagram, usage guide |
| **11.2** Security documentation | Threat model, security controls implemented |
| **11.3** Update memory-bank | Final state of all context files |
| **11.4** Update copilot-instructions | All learnings captured |

### Future: Phase 12+ (Not in Scope)

- **Play Mode** — Switch from Creator to Player, hybrid input (choices + free-form)
- **Export** — Stories as JSON/markdown/playable HTML or shareable links
- **Entra ID** — Add Microsoft Entra ID as production auth (additive to local Identity)
- **Local models** — Add Ollama for Critic + Archivist agents (privacy, offline)
- **More agents** — Add Weaver (plot) + CastDirector (characters) to Writer's Room
- **MAUI client** — Add .NET MAUI client alongside Blazor WASM
- **Console client** — Minimal console client for CLI/scripting scenarios
- **Azure APIM** — Add API Management GenAI Gateway
- **Redis sessions** — Replace in-memory with Redis
- **MCP tools** — Model Context Protocol integration

## Backend Server Component Breakdown

```
SecureProxyChatClients.Server          ← SECURE AUGMENTING PROXY (auth, tools, filtering — no game logic, no agents)
│
├── Program.cs                         ← Composition root
│   ├── AddIdentityApiEndpoints()     ← ASP.NET Core Identity (auto /register, /login, /manage)
│   ├── AddAuthentication()            ← Local + Entra ID dual auth
│   ├── AddRateLimiter()               ← Per-user rate limiting
│   ├── AddChatClient()                ← IChatClient (CopilotCli/AzureOpenAI/Fake via config)
│   ├── AddConversationStore()         ← IConversationStore (in-memory/Redis)
│   ├── AddServerTools()               ← Register server-side AIFunctions
│   └── MapEndpoints()                 ← All API routes
│
├── Endpoints/
│   ├── ChatEndpoints.cs
│   │   ├── POST /api/chat             ← Non-streaming chat (forwards to IChatClient with server tools)
│   │   └── POST /api/chat/stream      ← SSE streaming chat
│   └── (Identity API endpoints auto-mapped: /register, /login, /manage/*)
│
├── Data/
│   └── AppDbContext.cs                ← Identity DbContext (SQLite)
│
├── Middleware/
│   ├── RoleStrippingMiddleware.cs      ← Forces user role on all client messages
│   ├── InputValidationMiddleware.cs    ← Length, format, content validation
│   └── ContentFilteringMiddleware.cs   ← Sanitize AI output before sending
│
├── Services/
│   ├── ChatProxyService.cs            ← Core proxy logic (IChatClient → stream → SSE)
│   ├── ConversationStore.cs           ← IConversationStore (in-memory, pluggable)
│   └── CopilotCliChatClient.cs        ← Dev-time IChatClient via copilot CLI
│
├── Tools/                             ← Server-side AIFunctions (GenerateScene, CreateCharacter, etc.)
│
└── Auth/
    ├── PatAuthenticationHandler.cs
    └── DualAuthConfiguration.cs
```

## Key Technical Decisions

### Chat Protocol (Client ↔ Server)

```
POST /api/chat/stream
Content-Type: application/json
Authorization: Bearer <token>

{
  "messages": [...],                                // client builds context window (authoritative)
  "sessionId": "abc-123",                          // optional: for server-side persistence/audit/resume
  "clientTools": [{"name": "ToolA", "description": "...", "parameters": {...}}, ...],  // full schemas so server can pass to AI
  "options": {
    "streaming": true,
    "responseFormat": { "$ref": "SchemaName" }      // optional: structured output
  }
}
```

Response (SSE):
```
:keepalive

event: text-delta
id: evt-001
data: {"content": "partial response text"}

event: tool-call-request
id: evt-002
data: {"toolName": "ToolA", "args": {"param": "value"}, "callId": "tc-1", "continuationToken": "ct-abc-123"}

event: done
id: evt-003
data: {"sessionId": "abc-123", "usage": {"promptTokens": 100, "completionTokens": 50}}
```

**Protocol notes:**
- Client sends `messages` array (client is authoritative for context window); `sessionId` is for server-side persistence/audit only
- All events have `id` fields for deduplication/resume
- Heartbeat comments (`:keepalive`) sent every 15s during active streams
- `tool-call-request` includes `continuationToken` and **closes the stream** — client resumes with new POST
- Stream hard timeout: 5 min (configurable)
- No `agent-message` event — server has no agent knowledge; agent attribution is handled client-side
- Structured output comes through normal `text-delta` events as JSON text; client deserializes after `done`
- Consumed via `HttpClient` with `ResponseHeadersRead` (not browser `EventSource` — EventSource only supports GET)

### Tool Routing (Client vs Server Tools)

Since agents run on the CLIENT, tool routing works differently than a server-agent model:

**Server tool call flow:**
1. Agent (in WASM) calls `ProxyChatClient.GetResponseAsync()` with messages
2. Server forwards to Azure OpenAI via real `IChatClient`
3. AI responds with a tool call for a server tool (e.g. `GenerateScene`)
4. Server recognizes it as a registered server tool → executes it
5. Server feeds tool result back to AI → gets final response
6. Server returns final response to client

**Client tool call flow (stateless loop):**
1. Agent (in WASM) calls `ProxyChatClient.GetResponseAsync()` with messages
2. Server forwards to Azure OpenAI via real `IChatClient`
3. AI responds with a tool call for a client tool (e.g. `GetStoryGraph`)
4. Server does NOT recognize it as a server tool → returns the raw tool call response to client
5. `ProxyChatClient` sees the tool call, checks if it's a registered client tool
6. Client executes the tool locally (reads IndexedDB)
7. `ProxyChatClient` sends a NEW request with the tool result appended to messages
8. Server forwards to Azure OpenAI → gets final response → returns to client
9. `ProxyChatClient` repeats until no more tool calls (loop)

**Key insight**: `ProxyChatClient` is NOT a thin HTTP wrapper — it's a **tool-aware loop** that transparently handles client tool calls before returning to the caller. The agent doesn't know or care whether a tool ran on the server or client. See `docs/recommendations.md` for pseudocode.

### NuGet Packages (Anticipated)

**Server (secure augmenting proxy):**
- `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI`
- `Azure.AI.OpenAI`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Identity API endpoints)
- `Microsoft.EntityFrameworkCore.Sqlite` (Identity storage)
- `Microsoft.Identity.Web` (Entra ID)
- `Microsoft.AspNetCore.RateLimiting`

**Client (Blazor WASM — agents + game logic):**
- `Microsoft.Extensions.AI` (for `IChatClient` interface)
- `Microsoft.Agents.AI` (Agent Framework — ChatClientAgent, GroupChatOrchestrator)
- `Microsoft.AspNetCore.Components.WebAssembly`
- `Microsoft.AspNetCore.Components.WebAssembly.Authentication` (Bearer token auth)

**Shared:**
- `System.Text.Json`

**AppHost:**
- `Aspire.Hosting.AppHost`

### Authentication: Bearer Tokens

- Identity API endpoints issue bearer tokens on `/login`
- `ProxyChatClient` attaches `Authorization: Bearer <token>` to every request
- Blazor WASM stores token in memory (simplest, most secure for v1; localStorage is a future option)
- SSE streaming requests include the bearer token in the initial POST headers
- Seeded test user available in dev for Playwright tests

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| SSE stream timeout during client tool flow | Continuation-token multi-turn (close stream, new POST to resume) |
| Blazor WASM can't use EventSource for POST+auth | HttpClient with ResponseHeadersRead for SSE streaming |
| WASM + server cross-origin | CORS policy + bearer tokens (no cookies/CSRF needed) |
| Agent Framework GroupChatOrchestrator may be preview | Phase 9 is self-contained; wrap behind own interfaces |
| Agent Framework may not support WASM runtime | Validate in Phase 9 spike; fallback: implement thin orchestrator manually |
| Entra ID requires Azure tenant for testing | Local auth works without Azure; Entra is Phase 12+ additive feature |
| Game scope creep | Hard rule: infrastructure first, game is thin layer only |
| .NET 10 / C# 14 not yet available | Fall back to .NET 9 / C# 13; architecture is version-independent |
| Token cost during testing | FakeChatClient for CI; synthetic mode for load testing |
| Missing cancellation on client disconnect | CancellationToken propagation from HTTP to Azure OpenAI call |

## References

- `docs/proposal.md` — Original proposal with three investigation options
- `docs/research.md` — Deep research on proxy architectures
- `docs/ag-ui-security-considerations.md` — AG-UI security guidelines
- `docs/lore-engine.md` — Game concept & design (showcase app)
- `docs/recommendations.md` — Code snippets and implementation patterns
- [Agent Framework — Group Chat Orchestration](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/orchestrations/group-chat)
- [Agent Framework — ChatClientAgent](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-types/chat-client-agent)
- [AG-UI .NET Samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/AGUI)
- [MEAI IChatClient docs](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)
- [Aspire Azure OpenAI integration](https://learn.microsoft.com/en-gb/dotnet/aspire/azureai/azureai-openai-integration)
- [ai-chat-aspire-meai-csharp sample](https://github.com/Azure-Samples/ai-chat-aspire-meai-csharp)
