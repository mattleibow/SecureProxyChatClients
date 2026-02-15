# Copilot Instructions — SecureProxyChatClients

> **Last updated**: 2026-02-16
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
11. **HttpClient streaming** — Blazor WASM uses HttpClient for SSE. NOTE: WASM does NOT support `StreamReader.ReadLineAsync()` over streams — read full response then parse SSE events
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
- 2026-02-13: `copilot -p "prompt" --model gpt-5-mini --available-tools ""` works for non-interactive AI calls; output includes a usage footer that must be stripped. Note: `--allow-all-tools` was removed for security.
- 2026-02-13: ProxyChatClient is NOT a thin HTTP wrapper — it's a tool-aware loop that handles client tool calls transparently before returning to the caller. The agent doesn't know if a tool ran on server or client.
- 2026-02-13: When server receives a tool call it doesn't recognize (client tool), it returns the raw tool call response — server does NOT need to know about client tools
- 2026-02-13: "Secure augmenting proxy" — server adds auth, security, tools, content filtering, and enriches/augments client requests before forwarding to AI. Not a passthrough.
- 2026-02-13: xUnit standardized for all test projects — Microsoft.Playwright.Xunit provides same PageTest base class; uses `[Fact]` + `IAsyncLifetime` instead of NUnit's `[Test]` + `[SetUp]`/`[TearDown]`
- 2026-02-13: Bearer tokens stored in memory (not localStorage) for v1 — most secure, simplest; token lost on refresh is acceptable for now
- 2026-02-14: Bearer tokens stored in sessionStorage — survives SPA navigation; cleared on tab close; better UX than pure in-memory
- 2026-02-13: Entra ID is additive — can be wired in at any phase without architectural changes; local Identity API endpoints provide complete auth story
- 2026-02-13: Three-model review (Gemini + Codex + Opus) catches different things: Gemini found state/protocol issues, Codex found scope contradictions, Opus found phase ordering problems
- 2026-02-13: CORS for WASM+server: use `AllowAnyMethod/AllowAnyHeader/WithOrigins` but NOT `AllowCredentials` (bearer tokens don't need credentials mode)
- 2026-02-13: Integration tests need auth setup too — seed user login in `InitializeAsync` and attach bearer token to HttpClient
- 2026-02-14: `dotnet new webapi --auth Individual` is NOT available in .NET 10 — must add Identity packages manually (Microsoft.AspNetCore.Identity.EntityFrameworkCore + EF Sqlite + EF Design)
- 2026-02-14: Blazor WASM HttpClient does NOT support `StreamReader.ReadLineAsync()` over HTTP streams — throws `net_http_synchronous_reads_not_supported`. Must read full response then parse SSE events.
- 2026-02-14: `AuthenticatedHttpMessageHandler.SendAsync` must call `authState.InitializeAsync()` to read token from sessionStorage — handler may be created before page sets token
- 2026-02-14: HttpClientFactory creates handler instances separately from page DI scope — `AddTransient<AuthenticatedHttpMessageHandler>()` is correct, NOT `AddScoped`
- 2026-02-14: Playwright E2E tests with sessionStorage auth: use full page reload (`page.GotoAsync`) not SPA navigation to ensure sessionStorage is read by handler on fresh page load
- 2026-02-14: Playwright `WaitForFunction` with JavaScript is more reliable than `WaitForSelectorAsync` for checking dynamic state (e.g., button enabled/disabled)
- 2026-02-13: ChatResponse constructor varies by MEAI version — use `new ChatResponse([message])` (list form) for compatibility
- 2026-02-15: CORS hardened: restrict to specific methods (GET/POST/OPTIONS) and headers (Content-Type, Authorization, Accept) — don't use AllowAnyMethod/AllowAnyHeader
- 2026-02-15: ChatEndpoints security: add null checks for userId, try/catch around tool invocation, tool result size limits (32KB), SSE error handling
- 2026-02-15: ContentFilter should use source-generated Regex (`[GeneratedRegex]` + `partial class`) for real XSS sanitization — removes script/iframe/event handlers/javascript: protocol
- 2026-02-15: CopilotCliChatClient needs ILogger as constructor param — use factory pattern in DI: `services.AddSingleton<IChatClient>(sp => new CopilotCliChatClient(sp.GetRequired...))`
- 2026-02-16: Pgvector.EntityFrameworkCore 0.3 CosineDistance is an EF extension method that throws "FunctionOnClient" if called outside LINQ — use raw SQL with `<=>` operator for cosine distance queries
- 2026-02-16: Aspire.Hosting.PostgreSQL package version must match Aspire.Hosting.AppHost version (both 9.4.2) — mismatched versions cause TypeLoadException on `get_Pipeline`
- 2026-02-16: Collection expressions `[]` cannot initialize `IReadOnlySet<string>` — use `new HashSet<string>()` explicitly
- 2026-02-16: Game engine pattern: server-side tools enforce ALL game mechanics (dice, items, HP, gold, XP, NPCs) — client can't cheat. NPC HiddenSecret stripped before sending to client.
- 2026-02-16: Achievement system: state-based achievements checked after tool execution; event-based achievements awarded by game events. Use IReadOnlySet for the unlocked check parameter.
- 2026-02-16: Blazor WASM AuthState race condition: multiple components calling InitializeAsync() concurrently can race — first sets s_initialized=true, second returns before JS interop completes. Fix: use a shared Task (s_initTask ??= InitializeCoreAsync()) so all callers await the same operation.
- 2026-02-16: In OnAfterRenderAsync, use `await InvokeAsync(StateHasChanged)` instead of bare `StateHasChanged()` — the latter may not trigger re-render properly in Blazor WASM's single-threaded context.
- 2026-02-16: Auth-guarded pages should use `_initialized` pattern: show spinner until OnAfterRenderAsync completes auth init, then check IsAuthenticated. This prevents flash of "Sign in" on full page reload.
- 2026-02-16: Playwright WASM tests: the Blazor WASM dev server (blazor-devserver.dll) caches framework files. After rebuilding, you MUST restart the client process or framework JS returns 404 (fingerprinted filenames change). Error: "Failed to fetch dynamically imported module: dotnet.*.js"
- 2026-02-16: Playwright WASM tests: use 90s timeout for first page load (WASM needs to download .NET runtime + assemblies). After initial load, SPA navigation is instant — use NavLink clicks, NOT page.goto() which triggers full WASM reload.
- 2026-02-16: Walkthrough test pattern: register on /register → SPA redirects to /play → all subsequent navigation via NavLink clicks (never page.goto). This avoids WASM re-download on each step.
- 2026-02-16: RollCheck modifier formula: (statValue - 10) / 2, matching D&D convention. AI must pass actual player stat value. Default statValue=10 gives modifier=0.
- 2026-02-16: Level-up uses while loop consuming XP per level (threshold = level * 100). Multiple levels can be gained from one large XP award.
- 2026-02-16: Event-based achievements (critical-hit, diplomat, first-contact) awarded in GameToolRegistry.ApplyToolResult. State-based achievements checked via Achievements.CheckAchievements.
- 2026-02-16: MaxMessages=50 (was 10). Client sends up to 20 messages (TakeLast(20)). Too low causes Error 400 after ~5 gameplay turns.
- 2026-02-16: X-Forwarded headers: do NOT clear KnownNetworks/KnownProxies (makes ALL sources trusted). Keep defaults (loopback), set ForwardLimit=2.
- 2026-02-16: Never log user content (memory/chat). Log metadata only (type, userId, length) to prevent sensitive data in logs.
- 2026-02-16: first-loot achievement threshold: Count > 3 (not >2). Characters start with 3 items (class weapon + shield/accessory + 2x potions counted as single slot).
- 2026-02-16: Middleware ordering: UseRateLimiter() MUST come AFTER UseAuthentication()/UseAuthorization() so per-user rate limiting can read bearer claims. Otherwise partitions by IP only.
- 2026-02-16: InMemoryStoryMemoryService is a singleton — must use Lock (System.Threading.Lock) for thread safety when accessing List<StoryMemory>. ConcurrentBag is not suitable (needs ordered enumeration).
- 2026-02-16: InputValidator must reject empty/whitespace user messages. Check string.IsNullOrWhiteSpace(message.Content) for role="user" messages specifically.
- 2026-02-16: secret-keeper achievement: awarded when NPC has a non-trivial HiddenSecret (not "None" or empty). Tracked in ApplyToolResult NpcResult handler.
- 2026-02-16: dragon-slayer achievement: awarded via CombatResult when CreatureName contains "Ancient Dragon" (case-insensitive). RecordCombatWin tool must be called by AI after defeating a creature.
- 2026-02-15: TakeItem must decrement Quantity instead of removing the whole InventoryItem. Remove the item only when Quantity reaches 0 (or is 1 and being used).
- 2026-02-15: Bestiary.GetEncounterCreature must handle empty result from GetCreaturesForLevel — fallback to Creatures[^1] to avoid Random.Next(0) crash at very high player levels.
- 2026-02-15: Twist endpoint concurrency: catch InvalidOperationException from SavePlayerStateAsync — achievement will be awarded on next request if concurrent conflict occurs.
- 2026-02-15: Player handbook stat modifiers MUST match code: D&D formula (stat-10)/2, not fixed +2/+1 values. Keep tables synced with actual game engine.
- 2026-02-15: Play endpoints must ONLY accept user-role messages from client. System prompt is server-injected. assistant/tool roles from clients in Play endpoints enable forged prompt injection.
- 2026-02-15: GiveItem must merge stacks: if same-name item exists (case-insensitive), increment Quantity. Otherwise add new entry. Prevents inventory clutter.
- 2026-02-15: Death check: HandlePlayAsync and HandlePlayStreamAsync must reject actions when playerState.Health <= 0. Dead players must start a new game.
- 2026-02-15: Stream state persistence: state must be saved AFTER the streaming try/catch block (not just inside functionCalls==0 branch). Covers max-tool-rounds exit and error paths.
- 2026-02-15: DC scale runs 1-30 (not 1-20). Code clamps difficulty to Math.Clamp(dc, 1, 30). Handbook must match.
- 2026-02-15: Bestiary DM prompt format: "XP {xp}, Gold {gold}" — no stray suffix characters. Previously had "XP {xp}g {gold}gp" which was misleading.
- 2026-02-15: Achievement docs must match code: "Critical Hit" (not "Crit Machine"), "Cartographer" requires 10 locations (not all 12).
