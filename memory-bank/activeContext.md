# Active Context

> **Last updated**: 2026-02-15

## Current Phase

**Phase 3 COMPLETE** — Phases 1–3 fully implemented and tested. 42 tests passing (28 unit, 4 integration, 10 Playwright E2E). All tested live: login → chat → send → stream → multi-turn works.

## What We Just Did

- **Phase 1: Foundation + Auth** (commit `ed678cb`)
  - Solution structure: AppHost, ServiceDefaults, Server, Client.Web, Shared, 4 test projects
  - Server with ASP.NET Identity (manual setup — `dotnet new webapi --auth Individual` broken in .NET 10)
  - SQLite database, seed test user, CORS configuration
  - Blazor WASM login page with bearer token auth
  - `AuthenticatedHttpMessageHandler` + `AuthState` (sessionStorage-backed)
  - Protected `/api/ping` endpoint
  - Aspire wiring (separate apps, separate ports)
  - Shared contracts (ChatRequest, ChatResponse, ChatStreamEvent, LoginRequest/Response, etc.)

- **Phase 2: Security + Chat Proxy** (commit `ffd1097`)
  - POST `/api/chat` endpoint with auth, role stripping, input validation, content filtering
  - `FakeChatClient` (deterministic, for tests) + `CopilotCliChatClient` (dev-time AI)
  - Config-based AI provider switching (`AI:Provider` in appsettings)
  - Security middleware: `InputValidator`, `ContentFilter`, `SecurityOptions`
  - `SystemPromptService` for server-side system prompt injection

- **Phase 3: Chat UI + Streaming** (commits `8fc91ac`, `c0b5034`)
  - Chat.razor with message display, auto-scroll, streaming text
  - SSE streaming via full-response-then-parse (not `StreamReader.ReadLineAsync` — broken in WASM)
  - 10 Playwright E2E tests (auth flows + chat flows)
  - `AuthState` refactored to use sessionStorage for WASM page navigation survival

## What We're Doing Next

- **Phase 4: Security Hardening**
  - Per-user rate limiting (`Microsoft.AspNetCore.RateLimiting`)
  - Message length limits enforcement
  - Content sanitization (XSS prevention on LLM output)
  - Format validation
  - Tool allowlisting groundwork

- **Phase 5: Server-Side Tools**
  - `AIFunction` registration on server (GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist)
  - Tool execution pipeline
  - Tool result injection into AI conversation

- Future: Phase 6 (Client Tools) → Phase 7 (Persistence) → Phase 8 (Structured Output) → Phase 9 (Multi-Agent) → Phase 10 (Game Polish) → Phase 11 (Docs)

## Key Technical Learnings

1. **`dotnet new webapi --auth Individual` doesn't work in .NET 10** — must add Identity manually (AddIdentityApiEndpoints + AddEntityFrameworkStores + MapIdentityApi)
2. **Blazor WASM doesn't support `StreamReader.ReadLineAsync()` over HTTP** — read full response then parse SSE events
3. **HttpClientFactory handlers need `AuthState.InitializeAsync()` in `SendAsync`** — to pick up sessionStorage token (DI resolves handler as transient, not from page scope)
4. **In WASM, AuthState must use sessionStorage** (not just in-memory) to survive page navigations
5. **Playwright tests sharing a fixture need full page reload** (not SPA nav) for sessionStorage-based auth to work
6. **`AddHttpMessageHandler<T>()` resolves T as transient from DI**, not from the page's CascadingParameter scope