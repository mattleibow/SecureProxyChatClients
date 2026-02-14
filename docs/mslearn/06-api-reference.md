# API Reference — Endpoints, Formats, and Security Pipeline

## Overview

The secure proxy exposes a RESTful API using ASP.NET Core Minimal APIs. All endpoints except authentication require Bearer token authentication and are subject to per-user rate limiting.

## Authentication Endpoints

These endpoints are provided by ASP.NET Core Identity and do not require authentication.

### POST /login

Authenticates a user and returns a Bearer token.

**Request body:**

| Field | Type | Required |
|-------|------|----------|
| `email` | string | Yes |
| `password` | string | Yes |

**Success response (200):**

| Field | Type | Description |
|-------|------|-------------|
| `tokenType` | string | Always `"Bearer"` |
| `accessToken` | string | JWT access token |
| `expiresIn` | integer | Token lifetime in seconds (3,600) |
| `refreshToken` | string | Token for obtaining new access tokens |

**Rate limiting:** 10 requests per minute per IP (fixed window).

### POST /register

Creates a new user account.

**Request body:**

| Field | Type | Required |
|-------|------|----------|
| `email` | string | Yes |
| `password` | string | Yes |

**Success response:** 200 with Identity confirmation details.

**Error responses:**

| Status | Cause |
|--------|-------|
| 400 | Password does not meet policy, email already registered |
| 429 | Rate limit exceeded |

## Chat Endpoints

All chat endpoints require Bearer token authentication.

### POST /api/chat

Sends messages to the AI and returns a complete response.

**Request body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `messages` | array | Yes | Chat messages with `role` and `content` |
| `sessionId` | string | No | Existing session ID (created if omitted) |
| `clientTools` | array | No | Client-side tool definitions |

**Success response (200):**

| Field | Type | Description |
|-------|------|-------------|
| `messages` | array | Response messages from the AI |
| `sessionId` | string | Session ID for continuation |

### POST /api/chat/stream

Sends messages and returns an SSE stream of incremental text.

**Request body:** Same as `/api/chat`.

**Response:** `text/event-stream` with the following event types:

| Event | Data | Description |
|-------|------|-------------|
| `text-delta` | `{ "text": "..." }` | Incremental text chunk |
| `error` | `{ "error": "..." }` | Stream error |
| `done` | `{ "sessionId": "..." }` | Stream complete |

## Session Endpoints

All session endpoints require Bearer token authentication. Users can only access their own sessions.

### POST /api/sessions

Creates a new conversation session.

**Success response (200):** `{ "sessionId": "<uuid>" }`

### GET /api/sessions

Lists all sessions belonging to the authenticated user.

**Success response (200):** Array of session summaries with `id`, `title`, and `updatedAt`.

### GET /api/sessions/{id}/history

Retrieves the conversation history for a session.

**Success response (200):** Array of messages with `role` and `content`.

**Error responses:**

| Status | Cause |
|--------|-------|
| 403 | Session belongs to a different user |
| 404 | Session not found |

## Play Endpoints

Play endpoints power the interactive fiction game. All require Bearer token authentication.

### POST /api/play

Sends a game action to the AI with game state context.

**Request body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `messages` | array | Yes | Player action messages |
| `sessionId` | string | Yes | Active game session |
| `gameState` | object | No | Current player state |

**Success response (200):**

| Field | Type | Description |
|-------|------|-------------|
| `messages` | array | AI narrative response |
| `sessionId` | string | Session identifier |
| `gameState` | object | Updated player state |

### POST /api/play/stream

Streams a game response via SSE. Request format and event types match `/api/chat/stream`.

### GET /api/play/state

Returns the current player state (health, gold, XP, level, inventory).

### POST /api/play/new-game

Starts a new game, resetting player state to defaults.

### GET /api/play/twist

Returns a random "Twist of Fate" narrative event with title, prompt, emoji, and category.

### GET /api/play/achievements

Returns the player's unlocked achievements.

### POST /api/play/oracle

Submits a question to the Oracle NPC and returns a narrative answer.

**Request body:** `{ "question": "...", "sessionId": "..." }`

### GET /api/play/map

Returns the world map data with locations and connections.

### GET /api/play/encounter

Generates a random encounter appropriate for the player's level.

## Memory Endpoints

Memory endpoints provide semantic memory storage. All require Bearer token authentication.

### POST /api/memory/store

Stores a memory entry associated with the authenticated user.

**Request body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `content` | string | Yes | Memory content |
| `memoryType` | string | Yes | Classification (alphanumeric, hyphens, underscores) |
| `sessionId` | string | No | Associated session |
| `metadata` | object | No | Additional metadata |

**Success response (200):** `{ "id": "...", "stored": true }`

**Validation:** `memoryType` and `sessionId` must match `^[a-zA-Z0-9_-]+$`.

### GET /api/memory/recent

Retrieves recent memories for the authenticated user.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sessionId` | string | — | Filter by session |
| `limit` | integer | 10 | Maximum results |

**Success response (200):** `{ "results": [{ "id", "content", "memoryType", "createdAt" }] }`

## Health Endpoint

### GET /api/ping

Returns the authenticated user's identity. Requires Bearer token.

**Success response (200):** `{ "user": "<email>", "authenticated": true }`

## Error Responses

All error responses follow consistent patterns:

| Status | Meaning | Common Causes |
|--------|---------|---------------|
| 400 | Bad Request | Invalid input, message exceeds 4,000 characters, invalid session ID format, prompt injection detected |
| 401 | Unauthorized | Missing or invalid Bearer token |
| 403 | Forbidden | Accessing another user's session or memory |
| 404 | Not Found | Session does not exist |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unhandled exception (details suppressed via `GlobalExceptionHandler`) |

Server errors return RFC 9457 ProblemDetails responses without stack traces or internal implementation details.

## Rate Limiting

Two rate limiting policies protect the API:

| Policy | Strategy | Limit | Partition | Applies To |
|--------|----------|-------|-----------|------------|
| `chat` | Token bucket | 30 tokens / 60 seconds | Authenticated user ID (IP fallback) | All `/api/*` endpoints |
| `auth` | Fixed window | 10 requests / 60 seconds | Source IP | `/login`, `/register` |

Rate limits are configurable via `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds`.

## Security Pipeline Per Request

Every API request passes through these security layers, in order:

1. **Security headers** — CSP, X-Frame-Options, COEP added to response
2. **Audit logging** — 401/403 responses logged for security monitoring
3. **HTTPS enforcement** — HTTP redirected to HTTPS
4. **CORS validation** — Origin checked against configured client URL
5. **Rate limiting** — Token bucket or fixed window check
6. **Authentication** — Bearer token validated
7. **Authorization** — Endpoint access policy enforced
8. **Input validation** — Message count, length, total size, role stripping
9. **Prompt injection detection** — Blocked patterns checked
10. **Tool allowlisting** — Client tool names validated against allowlist
11. **System prompt injection** — Server prepends system prompt
12. **Content filtering** — Response sanitized before delivery

## Request Body Limits

| Limit | Value | Enforced By |
|-------|-------|-------------|
| Maximum request body | 1 MB | Kestrel server configuration |
| Maximum messages per request | 10 | Input validation |
| Maximum characters per message | 4,000 | Input validation |
| Maximum total characters | 50,000 | Input validation |
| Maximum tool result size | 32 KB | Tool result validation |

## Next Steps

- [Testing](07-testing.md) — How endpoints are tested across all layers
- [Deployment](08-deployment.md) — Production deployment and configuration
