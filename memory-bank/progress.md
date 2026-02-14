# Progress

> **Last updated**: 2026-02-15

## Completed

### Planning & Design
- [x] Read and analyzed proposal (`docs/proposal.md`)
- [x] Downloaded AG-UI security considerations doc
- [x] Researched .NET 10, C# 14, Aspire, MEAI, Agent Framework
- [x] Explored 5 game concepts, selected LoreEngine
- [x] Designed Writer's Room — 3 agents (Storyteller, Critic, Archivist), Creation Mode only
- [x] Established architecture: agents on CLIENT, server is secure augmenting proxy
- [x] Set up memory-bank structure + copilot-instructions
- [x] Created comprehensive 11-phase plan (auth-first, separate apps)
- [x] 3 rounds of 3-model review (Gemini, Codex, Opus) — all CRITICAL/HIGH resolved
- [x] Created `docs/recommendations.md` with code patterns
- [x] All key decisions documented (30+ in decision log)

### Phase 1: Foundation + Auth ✅ (commit `ed678cb`)
- [x] .NET solution structure (AppHost, ServiceDefaults, Server, Client.Web, Shared, 4 test projects)
- [x] Server with Identity API endpoints + SQLite + CORS (manual Identity setup — template broken in .NET 10)
- [x] Seed test user (`SeedDataService`)
- [x] Blazor WASM login page (Login.razor → server /login API → bearer token)
- [x] `AuthenticatedHttpMessageHandler` + `AuthState` (sessionStorage-backed)
- [x] Protected GET /api/ping endpoint + Ping.razor page
- [x] Aspire wiring (separate apps, separate ports)
- [x] Shared contracts (ChatRequest, ChatResponse, ChatStreamEvent, LoginRequest/Response, ChatMessageDto, ChatOptionsDto, ToolDefinitionDto, UsageDto)

### Phase 2: Basic Chat (Proxy + AI) ✅ (commit `ffd1097`)
- [x] Server chat endpoint (POST /api/chat, requires auth)
- [x] `CopilotCliChatClient` (real AI via copilot CLI, dev only)
- [x] Config-based AI provider switching (`AI:Provider` → fake/copilot-cli/azure-openai)
- [x] `FakeChatClient` (deterministic, for tests)
- [x] Security middleware: `InputValidator`, `ContentFilter`, role stripping
- [x] `SystemPromptService` for server-side prompt injection
- [x] `SecurityOptions` configuration

### Phase 3: Chat UI + Streaming ✅ (commits `8fc91ac`, `c0b5034`)
- [x] Chat.razor (text input, send button, message display, auto-scroll)
- [x] SSE streaming (full-response-then-parse pattern for WASM compatibility)
- [x] 10 Playwright E2E tests (AuthFlowTests + ChatFlowTests)
- [x] `AuthState` refactored to sessionStorage for page navigation survival
- [x] Full page reload pattern in Playwright for sessionStorage-based auth

### Test Coverage: 42 tests passing
- 28 unit tests (`Tests.Unit`)
- 4 integration tests (`Tests.Integration`)
- 10 Playwright E2E tests (`Tests.Playwright`)

## Not Started (matches docs/plan.md)

### Phase 4: Security Hardening
- [ ] Per-user rate limiting (`Microsoft.AspNetCore.RateLimiting`)
- [ ] Message length limits enforcement
- [ ] Content sanitization (XSS prevention on LLM output)
- [ ] Format validation
- [ ] Tool allowlisting groundwork

### Phase 5: Server-Side Tools
- [ ] `AIFunction` registration on server (GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist)
- [ ] Tool execution pipeline
- [ ] Tool result injection into AI conversation

### Phase 6: Client-Side Tools
- [ ] Client tool routing via stateless loop in ProxyChatClient
- [ ] Client tool result validation (S8)
- [ ] Client-side `AIFunction` registration (GetStoryGraph, SearchStory, etc.)

### Phase 7: Conversation Persistence & Sessions
- [ ] `IConversationStore` interface + in-memory implementation
- [ ] Server-generated session IDs + ownership verification
- [ ] Persist history for audit/resume

### Phases 8–11: See docs/plan.md
- Phase 8: Structured Output
- Phase 9: Multi-Agent Orchestration
- Phase 10: Game Polish (LoreEngine Creation Mode)
- Phase 11: Documentation & Polish
