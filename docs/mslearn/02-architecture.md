# Architecture — Secure AI Proxy Design

## System Overview

The secure AI proxy follows the **Backend-for-Frontend (BFF)** pattern, where the server acts as an intermediary between untrusted clients and AI providers. This architecture ensures that sensitive credentials and security policies are enforced at the server level.

## Trust Boundaries

The system has three distinct trust zones:

### 1. Untrusted Zone — Client Application

The Blazor WebAssembly client represents **any untrusted client** (mobile app, desktop app, third-party integration). The server makes no assumptions about client integrity:

- Client code can be inspected, modified, or replicated
- All data from the client is treated as untrusted input
- Authentication tokens are the only client-held credential
- Client-side tools execute in the browser sandbox

### 2. Trusted Zone — Server (BFF Proxy)

The ASP.NET Core server is the **security enforcement point**:

- Holds all AI provider credentials (API keys, endpoints)
- Enforces authentication and authorization
- Validates and sanitizes all input
- Filters all AI output before sending to clients
- Manages session state and conversation history
- Executes server-side tools with validated parameters
- Applies rate limiting per user

### 3. External Zone — AI Provider

Azure OpenAI is treated as a **trusted but external dependency**:

- Accessed only from the server using managed credentials
- Subject to timeout and error handling
- Responses are filtered before forwarding to clients

## Component Architecture

### Server Components

| Component | Responsibility |
|-----------|---------------|
| **Authentication Middleware** | Validates Bearer tokens, extracts user identity |
| **Rate Limiter** | Per-user token bucket for AI endpoints, fixed window for auth |
| **Input Validator** | Message count/length limits, role stripping, prompt injection detection |
| **Content Filter** | Strips script/iframe/object/embed tags, event handlers, javascript: URIs |
| **Chat Endpoints** | Proxies chat requests with tool execution loop |
| **Play Endpoints** | Game-specific AI interactions with state tracking |
| **Session Endpoints** | Conversation CRUD with ownership validation |
| **Memory Endpoints** | Vector-based story memory storage and retrieval |
| **Game Engine** | Server-side tools (combat, inventory, NPCs, achievements) |
| **Conversation Store** | SQLite/EF Core persistence for chat history |
| **Story Memory** | PostgreSQL pgvector for semantic memory search |

### Client Components

| Component | Responsibility |
|-----------|---------------|
| **Auth State** | Bearer token management (in-memory storage) |
| **Proxy Chat Client** | IChatClient implementation that routes through the server |
| **Client Tools** | Browser-side tools (dice rolling, local storage) |
| **Lore Agent** | Multi-agent orchestration (Storyteller, Critic, Archivist) |

## Data Flow — Chat Request

1. Client sends `POST /api/chat` with Bearer token and message array
2. Server validates the token and extracts the user identity
3. Rate limiter checks the user's token bucket
4. Input validator checks message count, lengths, and content
5. Server prepends the system prompt (client never sees it)
6. Server calls Azure OpenAI with the full message context
7. If AI requests tool calls, server executes them (or returns client tool calls)
8. Content filter sanitizes the AI response
9. Response is persisted to the conversation store
10. Filtered response is returned to the client

## Data Flow — Streaming

Streaming uses **Server-Sent Events (SSE)** for real-time token delivery:

1. Same validation pipeline as non-streaming (steps 1–5 above)
2. Server opens an SSE connection (`text/event-stream`)
3. Each token from Azure OpenAI is individually content-filtered
4. Filtered tokens are emitted as `text-delta` events
5. After streaming completes, the full response is re-filtered and persisted
6. A `done` event signals stream completion

The double-filtering approach (per-chunk + final) prevents **split-tag XSS attacks** where malicious content is distributed across multiple stream chunks.

## Session Management

Sessions provide conversation isolation:

- Each session has a unique ID and an owner (user ID)
- Session ownership is validated on every access
- Users can only see and access their own sessions
- Session history is stored server-side (never on the client)
- Auto-generated titles from the first user message

## Configuration Architecture

Configuration follows the standard ASP.NET Core configuration pipeline:

```
appsettings.json → appsettings.{Environment}.json → Environment Variables → secrets.json
```

Environment variables override file-based configuration, enabling secure deployment without secrets in source control. The `secrets.json` file is gitignored and never committed.

## Next Steps

- [Security](03-security.md) — Deep dive into the 20 security controls
- [Authentication](04-authentication.md) — Identity setup and session isolation
