# System Patterns

> **Last updated**: 2026-02-15

## Architecture Pattern

```
┌──────────────────────────────┐       ┌──────────────────────────┐       ┌─────────────────┐
│  Blazor WASM (Client)         │ HTTPS │  ASP.NET Core Server     │ HTTPS │  Azure OpenAI   │
│  (separate app, login only)   │ CORS  │  (Secure Augmenting Proxy)│◄─────►│  (AI Provider)  │
│                               │◄─────►│                          │       │                 │
│  AGENTS LIVE HERE:            │       │  - Identity UI (register)│       │                 │
│  - GroupChatOrchestrator      │       │  - Identity API (login)  │       │                 │
│  - Storyteller Agent ─────────┼───────┼─→ POST /api/chat ────────┼───────┤                 │
│  - Critic Agent ──────────────┼───────┼─→ (same endpoint) ───────┼───────┤                 │
│  - Archivist Agent ───────────┼───────┼─→ (same endpoint) ───────┼───────┤                 │
│    (all use ProxyChatClient)  │       │                          │       │                 │
│                               │       │  - Role stripping        │       │                 │
│  Client Tools (local):        │       │  - Rate limiting         │       │                 │
│  - GetStoryGraph              │       │  - Content filtering     │       │                 │
│  - SearchStory                │       │  - Input validation      │       │                 │
│  - SaveStoryState             │       │                          │       │                 │
│  - RollDice                   │       │  Server Tools:           │       │                 │
│  - GetWorldRules              │       │  - GenerateScene         │       │                 │
│                               │       │  - CreateCharacter       │       │                 │
│  Story State (IndexedDB)      │       │  - AnalyzeStory          │       │                 │
│  Chat UI (Chat.razor)         │       │  - SuggestTwist          │       │                 │
│  Login.razor (login only)     │       │                          │       │                 │
└──────────────────────────────┘       └──────────────────────────┘       └─────────────────┘
        ▲                                        ▲
        │  (separate origins, separate ports)     │
        └──────────── .NET Aspire AppHost ───────┘
```

## Key Patterns

### 1. IChatClient Proxy Pattern
- Client implements `IChatClient` via `ProxyChatClient` (HTTP → server)
- Server uses real `IChatClient` (Azure OpenAI SDK or CopilotCliChatClient for dev)
- Same interface on both sides of the boundary
- **Agents on client** use `ProxyChatClient` — each agent call goes through the proxy

### 2. Client-Side Agent Orchestration
- `GroupChatOrchestrator` + `ChatClientAgent` instances live in Blazor WASM
- Each agent has a distinct system prompt and personality
- Agents call `ProxyChatClient` for AI completions → server → OpenAI
- **Server has no knowledge of agents or game logic** — it's a secure augmenting proxy that adds auth, security, tools, and content filtering

### 3. Split Tool Execution
- **Server tools**: `AIFunction` registered on server, AI model requests them, server executes (GenerateScene, etc.)
- **Client tools**: `AIFunction` registered in client WASM, AI model requests them, client executes locally (GetStoryGraph, etc.)
- Client tools operate on local data (IndexedDB) — no server round-trip

### 4. Streaming via SSE (WASM-adapted)
- Server streams `ChatResponseUpdate` via Server-Sent Events
- Client consumes via HttpClient streaming (not EventSource — needs POST + auth headers)
- **WASM limitation**: `StreamReader.ReadLineAsync()` doesn't work over HTTP in Blazor WASM — read full response then parse SSE events
- Event IDs for deduplication, heartbeats every 15s, hard timeout

### 5. Dual Auth
- **v1 (dev + production)**: ASP.NET Core Identity API endpoints (manual setup — template broken in .NET 10) with SQLite
- **Phase 12+**: Microsoft Entra ID (OAuth 2.0 / OIDC) — additive
- Server→OpenAI: PAT from environment variable (v1); Entra managed identity (Phase 12+)

### 6. sessionStorage-Backed Auth State
- Bearer token stored in `sessionStorage` (not in-memory) to survive WASM page navigations
- `AuthState` service reads/writes via JS interop
- `AuthenticatedHttpMessageHandler` calls `AuthState.InitializeAsync()` in `SendAsync` (handler is DI-transient, not page-scoped)
- Cleared on tab close (more secure than localStorage)

### 7. Split State Management (planned)
- **Server**: Conversation state (IConversationStore — in-memory, pluggable to Redis) — Phase 7
- **Client**: Story/game state (IndexedDB — full story graph, characters, world rules) — Phase 9+

## Middleware Pipeline (Server — Secure Augmenting Proxy)

The server augments every client request through a security and enrichment pipeline:

```
Request → Auth → Rate Limiting → Role Stripping → Input Validation
    → [AUGMENT: inject system prompts, add server tool definitions, enrich context]
    → Server Tools + AI Provider (Azure OpenAI)
    → Content Filtering → Output Sanitization → SSE Stream → Response
```

**What "augmenting" means**: The server can add system prompts, inject context the client doesn't have, register server-side tools the AI can call, enforce output schemas, and filter/transform responses — all transparently to the client.

## Project Dependencies

```
AppHost
  ├── references → Server
  ├── references → Client.Web
  └── references → ServiceDefaults

Server (secure augmenting proxy — no game logic, adds auth/tools/filtering)
  ├── references → Shared
  └── references → ServiceDefaults

Client.Web (Blazor WASM — agents + game logic)
  └── references → Shared

Shared
  └── (no project references)

Tests.Unit       → Shared, Server, Client.Web
Tests.Integration → AppHost (Aspire.Hosting.Testing)
Tests.Playwright  → AppHost (Aspire.Hosting.Testing) + Microsoft.Playwright.Xunit
Tests.Smoke       → AppHost (Aspire.Hosting.Testing) + real OpenAI credentials
```
