# Decision Log

> **Last updated**: 2026-02-15

## Decisions

### 2026-02-13: App concept — LoreEngine (interactive fiction builder/player)
- **Context**: Needed a sample app that naturally exercises all capabilities
- **Decision**: LoreEngine — Writer's Room of AI agents builds interactive fiction, then you play it
- **Rationale**: Natural need for streaming (prose), server tools (generation), client tools (story state), structured output (scene/character schemas), multi-agent (group chat), auth (creative IP), sessions (story persistence)

### 2026-02-13: Cloud-only for v1, local models deferred
- **Context**: Could add Ollama for local agents (Critic, Archivist) now
- **Decision**: Cloud-only for v1, local models in a future phase
- **Rationale**: Focus on infrastructure, not model hosting complexity

### 2026-02-13: Infrastructure first, game second
- **Context**: Game could consume all the development time
- **Decision**: Each phase adds infrastructure capability first, thin game feature second
- **Rationale**: The reference sample (secure proxy) is the real deliverable

### ~~2026-02-13: Group Chat orchestration for Writer's Room~~ *(Superseded: now 3 agents — Storyteller, Critic, Archivist)*
- **Context**: Multiple Agent Framework patterns available (sequential, concurrent, handoff, group chat, magentic)
- **Decision**: ~~Group Chat — 5 agents (Scribe, CastDirector, Weaver, Critic, Archivist) collaborate~~ → 3 agents (Storyteller, Critic, Archivist)
- **Rationale**: Most natural fit — agents debate and refine story like a real writer's room

### ~~2026-02-13: Hybrid Play Mode input~~ *(Superseded: Play Mode deferred to Phase 12+)*
- **Context**: Three options: structured choices only, free-form only, or hybrid
- **Decision**: ~~Hybrid — structured choices displayed + free-form text accepted~~ → Creation Mode only in v1
- **Rationale**: ~~Best of both worlds~~ → Simplify v1 scope

### 2026-02-13: Blazor WASM replaces console client
- **Context**: Console app can't be automated with Playwright; want full E2E browser testing
- **Decision**: Standalone Blazor WASM as the untrusted client, drop console entirely
- **Rationale**: Playwright can drive the full UI (type, click, verify DOM), same untrusted trust model, much better test automation

### 2026-02-13: Four-layer testing strategy
- **Context**: Need automated validation after each phase
- **Decision**: Unit (FakeChatClient) → Integration (Aspire HTTP) → Playwright (browser E2E) → Smoke (real OpenAI)
- **Rationale**: Playwright is the key layer — Copilot can run it after each phase to verify work; FakeChatClient for CI speed; real OpenAI for final validation

### ~~2026-02-13: Console client first, MAUI deferred~~ *(Superseded: Blazor WASM replaces console)*
- **Context**: Proposal targets MAUI but we need to iterate fast
- **Decision**: ~~Console app~~ → Standalone Blazor WASM (enables Playwright E2E testing)
- **Rationale**: Playwright can drive the full UI; same untrusted trust model

### 2026-02-13: Custom chat API, not OpenAI-compatible surface
- **Context**: Could expose an OpenAI-compatible API (any SDK talks to it) or custom API
- **Decision**: Custom endpoints we control
- **Rationale**: We never give the OpenAI endpoint to the client; our API is the contract

### 2026-02-13: Dual auth — local + Entra ID
- **Context**: Need auth that works for dev and production
- **Decision**: ASP.NET local auth for dev, Entra ID for production, both supported simultaneously
- **Rationale**: Developers need to F5 without Azure setup; production needs enterprise auth

### 2026-02-13: Server→OpenAI auth via Entra ID + PAT fallback
- **Context**: Server needs to authenticate to Azure OpenAI
- **Decision**: Entra ID managed identity preferred, PAT via environment variable as fallback
- **Rationale**: Entra ID is production-ready; PAT allows local dev without managed identity

### 2026-02-13: Borrow AG-UI patterns, don't depend on AG-UI packages
- **Context**: AG-UI has great code patterns but is preview with ~25 NuGet deps
- **Decision**: Copy useful code, don't take package dependency
- **Rationale**: Avoids preview package risk, binary size impact, and supply chain risk

### ~~2026-02-13: Both server-managed and client-managed state~~ *(Superseded: client authoritative for context, server persists for audit)*
- **Context**: Server-managed is more secure; client-managed is simpler
- **Decision**: ~~Support both, experiment with both~~ → Client sends `messages` (authoritative for context window); server persists history for audit/resume
- **Rationale**: With agents on client, client curates scene-scoped context from IndexedDB

### 2026-02-13: Aspire orchestrates everything
- **Context**: Could have separate launch profiles
- **Decision**: Single AppHost, F5 starts everything
- **Rationale**: Best developer experience, matches Aspire's intended use

### 2026-02-13: Memory banks + copilot-instructions for context persistence
- **Context**: Need to maintain context across sessions with Gemini and Codex
- **Decision**: `.github/copilot-instructions.md` for rules, `memory-bank/` for context
- **Rationale**: Standard pattern, works across multiple AI tools

### 2026-02-13: Continuation-token multi-turn for client tool flow
- **Context**: Gemini + Codex both flagged hanging SSE during tool calls as a critical risk (LB timeouts, RAM holding)
- **Decision**: Server sends `tool-call-request` with `continuationToken` and closes the stream. Client starts new POST with tool result + token.
- **Rationale**: Stateless server, no idle connections, works with load balancers, supports multiple replicas

### ~~2026-02-13: Server-authoritative state (client history is read-only)~~ *(Superseded: split state model)*
- **Context**: Client-managed sessions conflict with GroupChatOrchestrator internal state
- **Decision**: ~~Server is canonical for conversation + orchestrator state~~ → Client authoritative for context window (`messages`); server persists for audit/resume; orchestrator runs on client
- **Rationale**: With agents on client, client curates scene-scoped context from IndexedDB — server can't do this

### 2026-02-13: Blazor WASM uses HttpClient streaming (not EventSource)
- **Context**: Browser EventSource API only supports GET and cannot set Authorization headers. Our protocol uses POST + Bearer tokens.
- **Decision**: Use HttpClient with `HttpCompletionOption.ResponseHeadersRead` and stream parsing
- **Rationale**: Full control over HTTP method, headers, and body. Works with POST and auth headers.

### ~~2026-02-13: Same-origin hosting for WASM + BFF~~ *(Superseded: separate origins + CORS)*
- **Context**: Separate origins create CORS, CSRF, and Entra redirect URI issues
- **Decision**: ~~Serve Blazor WASM from BFF~~ → Separate apps on separate ports; CORS + bearer tokens
- **Rationale**: Separate apps better represent real-world deployment; bearer tokens eliminate CSRF

### ~~2026-02-13: Auth shape + input validation in Phase 1~~ *(Superseded: auth in Phase 1, validation in Phase 4)*
- **Context**: Gemini flagged auth at Phase 3 as too late; Codex agreed security is cross-cutting
- **Decision**: ~~Phase 1 includes input validation~~ → Phase 1 = auth shape only; Phase 4 = input validation
- **Rationale**: Auth boundary from day 1; validation after basic chat works

### ~~2026-02-13: State store abstraction (IConversationStore) in Phase 1~~ *(Superseded: moved to Phase 2)*
- **Context**: Both reviewers flagged missing persistence layer. In-memory state lost on restart.
- **Decision**: ~~Phase 1~~ → Phase 2 task 2.7. Interface + in-memory impl; persistence hardened in Phase 7.
- **Rationale**: Phase 1 focuses on auth; conversation state not needed until chat exists

### 2026-02-13: CopilotCliChatClient for dev-time AI (no Azure setup)
- **Context**: Setting up Azure OpenAI credentials is a barrier to getting started. The `copilot` CLI tool is already installed and authenticated via GitHub Copilot subscription.
- **Decision**: Create `CopilotCliChatClient` that shells out to `copilot -p "message" --model gpt-5-mini` for each request. Process-per-request. Dev/testing only.
- **Rationale**: Zero setup, uses existing subscription, gives real AI responses immediately. Three-tier IChatClient strategy: FakeChatClient (CI) → CopilotCliChatClient (dev) → Azure OpenAI (prod)

### 2026-02-13: Config-based AI provider switching
- **Context**: Need to swap between FakeChatClient, CopilotCliChatClient, and Azure OpenAI without code changes
- **Decision**: `appsettings` configuration `AI:Provider` field selects the IChatClient implementation at startup
- **Rationale**: Clean separation; any developer can run the app with `copilot-cli` provider, CI uses `fake`, production uses `azure-openai`

### 2026-02-13: ASP.NET Core Identity API endpoints for local auth
- **Context**: Need local auth for dev. Options: manual endpoints vs Identity API endpoints vs hardcoded dev user
- **Decision**: Use `.AddIdentityApiEndpoints()` with SQLite — auto-generates `/register`, `/login`, `/manage/*` REST endpoints
- **Rationale**: Production-like, well-tested, minimal code. Playwright can test real registration + login flows.

### 2026-02-13: Seeded test user + registration flow tests
- **Context**: Playwright needs an authenticated user. Options: seed at startup vs register each test
- **Decision**: Seed a known test user at startup for most tests. Dedicated Playwright tests exercise the full registration→login→chat flow.
- **Rationale**: Seeded user = fast + deterministic for most tests. Registration tests ensure the full signup path works end-to-end.

### 2026-02-13: Client tool routing via stateless loop in ProxyChatClient
- **Context**: When AI model requests a client tool, server can't execute it. Options: bidirectional stream (SignalR) or stateless HTTP loop.
- **Decision**: Stateless loop — server returns raw tool call response, `ProxyChatClient` executes client tools locally, sends new request with tool result. Loops until no more tool calls.
- **Rationale**: Aligns with HTTP-based architecture. ProxyChatClient becomes a tool-aware loop, transparent to callers. No WebSocket complexity.

### 2026-02-13: Bearer tokens for WASM→server auth
- **Context**: Options: cookies (SameSite) or bearer tokens for Blazor WASM + API auth
- **Decision**: Bearer tokens — Identity API endpoints issue tokens, ProxyChatClient attaches Authorization header
- **Rationale**: Standard SPA+API pattern. Works cleanly with HttpClient and SSE streaming. No CSRF issues.

### 2026-02-13: Assume Agent Framework works in WASM, fix later if not
- **Context**: Microsoft.Agents.AI may have reflection/AOT issues in Blazor WASM
- **Decision**: Proceed assuming it works. If it fails in Phase 9, implement lightweight orchestrator using plain IChatClient
- **Rationale**: Agents are Phase 9. Plenty of time to discover and fix. The orchestrator pattern is simple enough to DIY.

### 2026-02-13: Standardize on xUnit for all test projects
- **Context**: Microsoft.Playwright.Xunit package exists, enabling xUnit for Playwright E2E tests too.
- **Decision**: xUnit for all test projects — unit, integration, Playwright, and smoke.
- **Rationale**: Single test framework eliminates runner conflicts. xUnit is the .NET ecosystem default, uses `IAsyncLifetime` for setup/teardown, and `[Fact]`/`[Theory]` attributes.

### 2026-02-13: Bearer token stored in memory for v1
- **Context**: Options: in-memory (lost on refresh) or localStorage (persists). Affects security posture.
- **Decision**: In-memory for v1. localStorage is a Phase 12+ option.
- **Rationale**: Most secure option. Simplest implementation. Session survival will be addressed in future phases.

### 2026-02-13: Entra ID deferred to Phase 12+
- **Context**: R4 requires dual auth (local + Entra). Entra needs Azure tenant, complicates Phase 1.
- **Decision**: Defer Entra ID to Phase 12+. Local auth is sufficient for all development phases.
- **Rationale**: Local Identity API endpoints provide full auth story. Entra is additive and can be wired in later without architectural changes.

### 2026-02-13: Code snippets separated into recommendations doc
- **Context**: plan.md had ~350 lines of code mixed with requirements. Made the plan hard to read.
- **Decision**: Created `docs/recommendations.md` for all code snippets and patterns. plan.md references it.
- **Rationale**: Plan stays focused on requirements, architecture, and phases. Code lives in one place for easy reference.

### 2026-02-14: Client is authoritative for context window, server persists history
- **Context**: R8 said "server-authoritative" but agents on client build the context. Both Gemini and Opus flagged contradiction.
- **Decision**: Client sends `messages` array (authoritative for AI context window). Server persists history for audit/resume only. `sessionId` is for logging/persistence, not context loading.
- **Rationale**: With agents on client, the client curates scene-scoped context from IndexedDB — server can't do this.

### 2026-02-14: Remove `agent-message` from SSE protocol
- **Context**: Protocol had `event: agent-message` but server has no agent knowledge. Both Gemini and Opus flagged.
- **Decision**: Server emits only `text-delta`, `tool-call-request`, `done`. Agent attribution is client-side only.
- **Rationale**: Server is a proxy — it doesn't know which agent is speaking.

### 2026-02-14: S8 (client tool result validation) added to Phase 6 as task 6.5
- **Context**: Security requirement S8 had no implementing phase. Opus flagged as critical.
- **Decision**: Phase 6 task 6.5 implements server-side validation of client-provided tool results.
- **Rationale**: Must be in place before client tools go live.

### 2026-02-14: Export deferred to Phase 12+
- **Context**: Phase 10 had export task but lore-engine.md said export is future. Codex + Opus flagged.
- **Decision**: Export removed from Phase 10, added to Phase 12+ future list.
- **Rationale**: Aligns both docs on v1 scope boundary.

### 2026-02-14: Phase 7 renamed to "Conversation Persistence & Sessions"
- **Context**: "Session Management" implied server-authoritative state. Gemini flagged.
- **Decision**: Renamed to clarify: server persists history for audit/resume, client owns context.
- **Rationale**: Clearer intent matches the client-authoritative architecture.

### 2026-02-15: Manual Identity setup required in .NET 10
- **Context**: `dotnet new webapi --auth Individual` template is broken/unavailable in .NET 10 preview.
- **Decision**: Add Identity manually: `AddIdentityApiEndpoints<AppUser>()` + `AddEntityFrameworkStores<AppDbContext>()` + `MapIdentityApi<AppUser>()` with SQLite.
- **Rationale**: Only way to get Identity API endpoints working in .NET 10. Template may be fixed later.

### 2026-02-15: Full-response-then-parse for SSE in Blazor WASM
- **Context**: `StreamReader.ReadLineAsync()` over HTTP doesn't work in Blazor WASM (browser fetch limitation).
- **Decision**: Read the entire response body, then parse SSE events from the complete string.
- **Rationale**: Only reliable approach in WASM. Streaming appearance maintained via event-by-event UI updates after parsing.

### 2026-02-15: sessionStorage for bearer token (not in-memory)
- **Context**: Original plan was in-memory bearer token. In WASM, page navigations cause component re-initialization, losing in-memory state.
- **Decision**: Store bearer token in sessionStorage via JS interop. `AuthState` reads from sessionStorage on initialization.
- **Rationale**: Survives SPA page navigations and full page reloads within the same browser tab. Cleared on tab close (more secure than localStorage).

### 2026-02-15: AuthState.InitializeAsync() in HttpMessageHandler.SendAsync
- **Context**: `AddHttpMessageHandler<T>()` resolves T as transient from DI, not from the page's scope. The handler instance doesn't share the page's AuthState.
- **Decision**: Call `AuthState.InitializeAsync()` inside `SendAsync` to read the sessionStorage token before attaching the Authorization header.
- **Rationale**: Ensures the handler always has the current token regardless of DI scope. Works because sessionStorage is a shared browser-level store.

### 2026-02-15: Playwright full page reload for sessionStorage auth
- **Context**: Playwright tests sharing a fixture (same browser context) need auth state. SPA navigation doesn't trigger sessionStorage reads in new page components.
- **Decision**: Use `Page.GotoAsync(url)` (full page reload) instead of clicking SPA nav links when auth state needs to be picked up.
- **Rationale**: Full reload re-initializes all Blazor components, triggering `AuthState.InitializeAsync()` which reads from sessionStorage.

### 2026-02-15: Phases 2+3 merged in implementation
- **Context**: Plan had Phase 2 (Basic Chat) and Phase 3 (Streaming) as separate phases.
- **Decision**: Implemented chat endpoint with SSE streaming from the start. Phase 2 commit includes the proxy + security; Phase 3 commits include Chat UI + Playwright E2E.
- **Rationale**: Streaming was natural to implement alongside the chat endpoint. Splitting would have required a non-streaming endpoint that would be immediately replaced.
