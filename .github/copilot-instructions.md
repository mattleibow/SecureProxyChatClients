# Copilot Instructions — SecureProxyChatClients

> **Last updated**: 2026-02-13
> **HARD RULE**: After each meaningful unit of work, update this file AND the memory-bank files. All learnings go here, not in GitHub Memories.

## Project Identity

- **Name**: SecureProxyChatClients
- **Purpose**: Secure AI proxy reference sample — a BFF (Backend-for-Frontend) that mediates between untrusted clients and Azure OpenAI
- **Stack**: .NET 10, C# 14, .NET Aspire, Microsoft.Extensions.AI (MEAI), ASP.NET Core

## Architecture Rules

1. **Never expose OpenAI credentials to the client** — the server owns all AI provider keys/tokens
2. **The client is untrusted BUT smart** — agents + game logic live in Blazor WASM; server is a secure augmenting proxy
3. **IChatClient on both sides** — agents use `ProxyChatClient` → server → Azure OpenAI; same interface everywhere
4. **Agents on the CLIENT** — `GroupChatOrchestrator` + `ChatClientAgent` instances run in Blazor WASM, NOT on the server
5. **Server is a SECURE AUGMENTING PROXY** — no agents, no orchestration, no game state; handles auth, security, rate limiting, domain tools (GenerateScene etc.), content filtering, and enriches client requests before forwarding to AI
6. **Conversation persistence on server** — `IConversationStore` persists history for audit/resume; client is authoritative for context window (`messages` payload); story/game state in client IndexedDB
7. **Aspire orchestrates everything** — F5 starts AppHost which launches server + Blazor WASM as separate apps on separate ports
8. **Separate apps, separate origins** — Server is a web app with Identity UI (registration); WASM is a plain standalone app (login only, no registration). CORS configured for cross-origin API calls.
9. **Server tools** = AIFunctions the AI model requests, executed on server (GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist)
10. **Client tools** = AIFunctions the AI model requests, executed locally in WASM (GetStoryGraph, SearchStory, SaveStoryState, RollDice, GetWorldRules)
11. **HttpClient streaming** — Blazor WASM uses HttpClient with ResponseHeadersRead for SSE (not EventSource)
12. **Same-origin NOT required** — WASM and server are separate origins; use CORS + bearer tokens
13. **Auth shape from Phase 1** — Identity API endpoints + registration UI on server from day 1; WASM has login-only page

## Tech Stack & Versions

| Component | Version | Notes |
|-----------|---------|-------|
| .NET | 10.0 (LTS) | Use latest TFM `net10.0` |
| C# | 14 | Use extension members, field keyword, null-conditional assignment, etc. |
| Aspire | Latest stable | `Aspire.Hosting.*` packages |
| MEAI | Latest stable | `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` |
| ASP.NET Core | 10.0 | Minimal APIs preferred |
| Auth | ASP.NET Core Identity API endpoints (v1); `Microsoft.Identity.Web` (Phase 12+ Entra ID) | Local auth for v1 |

## Coding Conventions

- **Minimal APIs** over controllers
- **Primary constructors** for DI
- **File-scoped namespaces**
- **Global usings** in each project
- **`IAsyncEnumerable`** for streaming endpoints
- **Records** for DTOs and tool parameters
- **Extension members** (C# 14) where they improve readability
- **`field` keyword** (C# 14) for property backing fields with validation
- **Nullable reference types** enabled everywhere
- **No `var` abuse** — use explicit types when the type isn't obvious from the right side

## Security Checklist (per AG-UI Security Doc)

- [ ] Role stripping — force all client messages to `role: user`
- [ ] Tool allowlisting — server controls which tools are available
- [ ] Input validation — message content length, format, schema
- [ ] State validation — JSON schema validation for any client state (Phase 6, S8)
- [ ] Content filtering — sanitize LLM output before sending to client
- [ ] Rate limiting — per-user, per-session
- [ ] Session management — server-generated thread IDs, ownership verification
- [ ] Sensitive data filtering — strip keys, PII, stack traces from tool responses
- [ ] Certificate pinning guidance — document for mobile clients (Phase 12+, when MAUI added)

## File Structure

```
SecureProxyChatClients/
├── .github/copilot-instructions.md    ← YOU ARE HERE
├── memory-bank/                        ← Persistent context files
├── docs/                               ← Research, proposal, security docs, game concept
├── src/
│   ├── SecureProxyChatClients.AppHost/    ← Aspire orchestrator
│   ├── SecureProxyChatClients.ServiceDefaults/ ← Shared Aspire defaults
│   ├── SecureProxyChatClients.Server/     ← ASP.NET Core secure augmenting proxy
│   ├── SecureProxyChatClients.Client.Web/ ← Blazor WASM (standalone, untrusted)
│   └── SecureProxyChatClients.Shared/     ← Shared contracts & models
├── tests/
│   ├── SecureProxyChatClients.Tests.Unit/        ← Fast unit tests (FakeChatClient)
│   ├── SecureProxyChatClients.Tests.Integration/ ← Aspire HTTP-level tests
│   ├── SecureProxyChatClients.Tests.Playwright/  ← Browser automation E2E
│   └── SecureProxyChatClients.Tests.Smoke/       ← Real OpenAI validation
└── SecureProxyChatClients.sln
```

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-02-13 | Console client first, MAUI later | ~~Superseded: Blazor WASM replaces console~~ |
| 2026-02-13 | Blazor WASM replaces console | Enables Playwright E2E testing; still an untrusted client (runs in browser) |
| 2026-02-13 | ~~Server-authoritative state~~ → Split state model | Client authoritative for context window (`messages`); server persists history for audit/resume; story/game state in client IndexedDB |
| 2026-02-13 | Dual auth (local + Entra ID) | Local for dev, Entra for production; PAT via env var for server→OpenAI |
| 2026-02-13 | Aspire orchestrates everything | Single F5 experience |
| 2026-02-13 | Borrow AG-UI patterns, don't depend on AG-UI packages | Use proven patterns but avoid preview package risk |
| 2026-02-13 | Continuation-token multi-turn for tool calls | No hanging SSE; server closes stream, client resumes with new POST |
| 2026-02-13 | HttpClient streaming (not EventSource) | EventSource only supports GET; we need POST + auth headers |
| 2026-02-13 | Separate origins + CORS | Server and WASM are separate apps on separate ports; CORS + bearer tokens for cross-origin calls |
| 2026-02-13 | Auth shape in Phase 1; input validation in Phase 4 | Auth boundary from day 1; security hardening after basic chat works |
| 2026-02-13 | IConversationStore abstraction in Phase 2 | Interface + in-memory impl; persistence hardened in Phase 7 |
| 2026-02-13 | CopilotCliChatClient for dev AI | `copilot -p` gives real AI with zero Azure setup; default model gpt-5-mini |
| 2026-02-13 | Config-based AI provider switching | `AI:Provider` in appsettings selects IChatClient: `fake` / `copilot-cli` / `azure-openai` |
| 2026-02-13 | Identity API endpoints for local auth | `.AddIdentityApiEndpoints()` + SQLite — auto REST endpoints, production-like |
| 2026-02-13 | Seeded test user + registration tests | Seed known user for most Playwright tests; dedicated tests for full registration flow |
| 2026-02-13 | Agents on CLIENT (Blazor WASM) | GroupChatOrchestrator + ChatClientAgent in WASM; server is secure augmenting proxy |
| 2026-02-13 | 3 agents: Storyteller, Critic, Archivist | Simplified from 5; Creation Mode only, no Play Mode in v1 |
| 2026-02-13 | Client owns story data (IndexedDB) | Sends scene-scoped context to server; full graph stays local |
| 2026-02-13 | 4 server + 5 client tools | Server: GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist. Client: GetStoryGraph, SearchStory, SaveStoryState, RollDice, GetWorldRules |
| 2026-02-13 | CopilotCliChatClient JSON tool simulation | System prompt instructs model to output JSON for tool calls; client parses |
| 2026-02-13 | Stateless tool routing loop in ProxyChatClient | Server returns raw tool call; client executes locally, loops with new request |
| 2026-02-13 | Bearer tokens for WASM auth | Identity API endpoints issue tokens; ProxyChatClient attaches Authorization header |

## Learnings

_Updated as we go. Each entry is timestamped._

- 2026-02-13: Browser `EventSource` API only supports GET requests and cannot set custom headers — must use `fetch` + `ReadableStream` or `HttpClient` streaming for SSE with POST + Bearer auth
- 2026-02-13: Load balancers/firewalls have 2-4 min idle timeouts — never hold SSE connections open waiting for external callbacks
- 2026-02-13: Agent Framework agents on client is the right pattern — server is a secure augmenting proxy (auth, tools, filtering), client orchestrates agents
- 2026-02-13: Mixed test frameworks (xUnit + NUnit) increase CI discovery flakiness — prefer single framework or hard-isolate pipelines
- 2026-02-13: Microsoft.Playwright.Xunit exists — provides same `PageTest` base class as NUnit variant, uses `[Fact]` + `IAsyncLifetime` instead of `[Test]` + `[SetUp]`/`[TearDown]`
- 2026-02-13: `copilot -p "prompt" --model gpt-5-mini --available-tools "" --allow-all-tools` works for non-interactive AI calls; output includes a usage footer that must be stripped
- 2026-02-13: ProxyChatClient is NOT a thin HTTP wrapper — it's a tool-aware loop that handles client tool calls transparently before returning to the caller. The agent doesn't know if a tool ran on server or client.
- 2026-02-13: When server receives a tool call it doesn't recognize (client tool), it returns the raw tool call response — server does NOT need to know about client tools
- 2026-02-13: "Secure augmenting proxy" — server adds auth, security, tools, content filtering, and enriches/augments client requests before forwarding to AI. Not a passthrough.
- 2026-02-13: xUnit standardized for all test projects — Microsoft.Playwright.Xunit provides same PageTest base class; uses `[Fact]` + `IAsyncLifetime` instead of NUnit's `[Test]` + `[SetUp]`/`[TearDown]`
- 2026-02-13: Bearer tokens stored in memory (not localStorage) for v1 — most secure, simplest; token lost on refresh is acceptable for now
- 2026-02-13: Entra ID is additive — can be wired in at any phase without architectural changes; local Identity API endpoints provide complete auth story
- 2026-02-13: Three-model review (Gemini + Codex + Opus) catches different things: Gemini found state/protocol issues, Codex found scope contradictions, Opus found phase ordering problems
- 2026-02-13: CORS for WASM+server: use `AllowAnyMethod/AllowAnyHeader/WithOrigins` but NOT `AllowCredentials` (bearer tokens don't need credentials mode)
- 2026-02-13: Integration tests need auth setup too — seed user login in `InitializeAsync` and attach bearer token to HttpClient
- 2026-02-13: ChatResponse constructor varies by MEAI version — use `new ChatResponse([message])` (list form) for compatibility
