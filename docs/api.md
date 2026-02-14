# API Documentation

> SecureProxyChatClients Server API Reference

## Base URL

When running via Aspire, the server typically runs at `http://localhost:5167`. The Aspire dashboard shows the actual URLs.

---

## Authentication

The server uses ASP.NET Core Identity with bearer token authentication.

### Login

```
POST /login
```

**Request Body:**
```json
{
  "email": "test@test.com",
  "password": "Test123!"
}
```

**Response (200 OK):**
```json
{
  "tokenType": "Bearer",
  "accessToken": "CfDJ8...",
  "expiresIn": 3600,
  "refreshToken": "CfDJ8..."
}
```

**Usage:** Include the access token in all subsequent requests:
```
Authorization: Bearer CfDJ8...
```

### Register

```
POST /register
```

**Request Body:**
```json
{
  "email": "newuser@example.com",
  "password": "SecurePass123!"
}
```

> **Note:** Registration is handled by the server's Identity UI. The client app only supports login — registration is done via the server directly.

---

## Chat Endpoints

All chat endpoints require authentication.

### Send Message (Non-Streaming)

```
POST /api/chat
```

**Request Body:**
```json
{
  "messages": [
    { "role": "user", "content": "Tell me a story about a dragon" }
  ],
  "sessionId": "optional-session-id",
  "clientTools": [
    { "name": "GetStoryGraph", "description": "Gets the current story graph" }
  ],
  "options": {
    "responseFormat": "text"
  }
}
```

**Response (200 OK):**
```json
{
  "messages": [
    { "role": "assistant", "content": "Once upon a time, in a land far away..." }
  ],
  "sessionId": "auto-generated-or-provided-id"
}
```

**Response with Tool Calls (200 OK):**

When the AI model wants to call a client-side tool:
```json
{
  "messages": [
    {
      "role": "assistant",
      "content": null,
      "toolCalls": [
        {
          "callId": "call_abc123",
          "name": "GetStoryGraph",
          "arguments": {}
        }
      ]
    }
  ],
  "sessionId": "session-id"
}
```

The client should execute the tool locally, then send the result back in a follow-up request:
```json
{
  "messages": [
    { "role": "user", "content": "Tell me about the story" },
    {
      "role": "assistant",
      "toolCalls": [
        { "callId": "call_abc123", "name": "GetStoryGraph", "arguments": {} }
      ]
    },
    {
      "role": "tool",
      "toolCallId": "call_abc123",
      "content": "{\"scenes\": [], \"characters\": [], \"connections\": []}"
    }
  ]
}
```

### Send Message (Streaming via SSE)

```
POST /api/chat/stream
```

**Request Body:** Same as `/api/chat`

**Response:** Server-Sent Events (SSE) stream

```
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive
```

**Event Types:**

| Event | Data Format | Description |
|-------|-------------|-------------|
| `text-delta` | `{"text": "token"}` | Incremental text token |
| `done` | `{"sessionId": "..."}` | Stream complete |

**Example SSE Response:**
```
event: text-delta
data: {"text":"Once "}

event: text-delta
data: {"text":"upon "}

event: text-delta
data: {"text":"a time..."}

event: done
data: {"sessionId":"abc-123"}
```

---

## Session Endpoints

All session endpoints require authentication. Users can only access their own sessions.

### Create Session

```
POST /api/sessions
```

**Response (200 OK):**
```json
{
  "sessionId": "generated-uuid"
}
```

### List Sessions

```
GET /api/sessions
```

**Response (200 OK):**
```json
[
  {
    "id": "session-uuid",
    "title": "Story about dragons",
    "updatedAt": "2026-02-14T10:30:00Z"
  }
]
```

### Get Session History

```
GET /api/sessions/{id}/history
```

**Response (200 OK):**
```json
[
  { "role": "user", "content": "Tell me a story" },
  { "role": "assistant", "content": "Once upon a time..." }
]
```

**Error Responses:**
- `404 Not Found` — Session does not exist
- `403 Forbidden` — Session belongs to a different user

---

## Health / Connectivity

### Ping (Authenticated)

```
GET /api/ping
```

**Response (200 OK):**
```json
{
  "user": "test@test.com",
  "authenticated": true
}
```

---

## Error Responses

### Validation Error (400)
```json
{
  "error": "Message exceeds maximum length of 4000 characters."
}
```

### Unauthorized (401)
Returned when no valid bearer token is provided.

### Rate Limited (429)
Returned when the per-user rate limit (30 requests/60 seconds) is exceeded.

---

## Security Pipeline

Every request to `/api/chat` and `/api/chat/stream` passes through:

1. **Authentication** — Bearer token validated via ASP.NET Core Identity
2. **Rate limiting** — Fixed window per-user (30/60s default)
3. **Input validation** — Message count, length, total size limits
4. **Role stripping** — All user-authored messages forced to `role: user`
5. **Prompt injection detection** — Blocked patterns checked
6. **Tool allowlisting** — Only pre-approved client tool names accepted
7. **System prompt injection** — Server prepends its own system prompt
8. **Content filtering** — Output sanitized before returning to client

---

## Data Models

### ChatMessageDto
```json
{
  "role": "user | assistant | system | tool",
  "content": "message text",
  "authorName": "optional agent name",
  "toolCalls": [{ "callId": "...", "name": "...", "arguments": {} }],
  "toolCallId": "for tool result messages"
}
```

### ToolDefinitionDto
```json
{
  "name": "GetStoryGraph",
  "description": "Gets the current story graph from local storage",
  "parameters": null
}
```

### Server Tools (Executed Server-Side)

| Tool | Description |
|------|-------------|
| `GenerateScene` | Generates a new story scene |
| `CreateCharacter` | Creates a new character |
| `AnalyzeStory` | Analyzes story structure and quality |
| `SuggestTwist` | Suggests a plot twist |

### Client Tools (Executed in Browser)

| Tool | Description |
|------|-------------|
| `GetStoryGraph` | Retrieves story graph from local storage |
| `SearchStory` | Searches local story data |
| `SaveStoryState` | Saves scene content to local state |
| `RollDice` | Rolls dice for game mechanics |
| `GetWorldRules` | Retrieves world-building rules |
