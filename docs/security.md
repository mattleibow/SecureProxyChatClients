# Security guide for SecureProxyChatClients

This guide documents every security control implemented in the **SecureProxyChatClients** reference sample — a .NET 10 secure AI proxy that uses the Backend-for-Frontend (BFF) pattern to stand between untrusted Blazor WebAssembly clients and Azure OpenAI.

The architecture follows a zero-trust client principle: the WASM client is treated as a completely untrusted public surface. All secrets, system prompts, and AI service credentials remain on the server. Every piece of user input is validated, every AI response is sanitized, and every endpoint enforces authentication and ownership before processing a request.

## Architecture overview

```
┌──────────────────┐       HTTPS        ┌────────────────────────┐       ┌──────────────┐
│  Blazor WASM     │ ───────────────────►│  ASP.NET Core Server   │──────►│ Azure OpenAI │
│  (untrusted)     │  Bearer token auth  │  (BFF / secure proxy)  │       │              │
└──────────────────┘                     └────────────────────────┘       └──────────────┘
                                           │  Input validation
                                           │  Content filtering
                                           │  Rate limiting
                                           │  Session isolation
                                           │  Audit logging
```

The server acts as the sole gateway to the AI provider. Clients never communicate with Azure OpenAI directly, ensuring that API keys, system prompts, and model parameters are never exposed.

## Authentication and authorization

### Identity configuration

The server uses ASP.NET Core Identity with the **Bearer token** authentication scheme for all API endpoints. This is configured in `Program.cs` using `AddIdentityApiEndpoints<IdentityUser>` with the following policy:

| Setting | Value |
|---|---|
| Minimum password length | 12 characters |
| Require digit | Yes |
| Require non-alphanumeric | Yes |
| Require uppercase | Yes |
| Require lowercase | Yes |
| Email confirmation required | No |
| Max failed login attempts | 5 |
| Lockout duration | 15 minutes |

Account lockout protects against brute-force credential attacks by temporarily disabling accounts after five consecutive failed login attempts.

### Bearer-only API authentication

All API endpoints (`/api/chat`, `/api/sessions`, `/api/memory`, `/api/play`, `/api/ping`) require `IdentityConstants.BearerScheme`. This is a deliberate security design:

- **Bearer tokens are not sent automatically by browsers**, which eliminates the entire class of cross-site request forgery (CSRF) attacks. There is no ambient authority for an attacker to exploit.
- **Cookie authentication is disabled for API endpoints** — only Bearer token authentication is accepted.

This separation ensures that even if an attacker can induce a victim's browser to make a cross-origin request to an API endpoint, the request will not carry any authentication credential and will be rejected with `401 Unauthorized`.

### User identity extraction

User identity is always derived from the authenticated `ClaimsPrincipal`. All endpoints extract the user ID from `ClaimTypes.NameIdentifier` — never from client-supplied values such as query strings or request bodies. This prevents identity spoofing and ensures that authorization decisions are based on cryptographically verified claims.

## Input security

All client input passes through `InputValidator` before reaching any AI service. This class is registered as a singleton and configured through the `Security` section of `appsettings.json` via `SecurityOptions`.

### Message limits

| Limit | Default | Configurable |
|---|---|---|
| Maximum messages per request | 10 | `Security:MaxMessages` |
| Maximum characters per message | 4,000 | `Security:MaxMessageLength` |
| Maximum total characters across all messages | 50,000 | `Security:MaxTotalLength` |
| Maximum session ID length | 128 characters | Validated at endpoint level |

These limits prevent oversized payloads from consuming excessive server memory or AI provider tokens.

> **Configuration:** All input limits are defined in `SecurityOptions.cs` and can be adjusted via `appsettings.json` or environment variables without code changes.

### System prompt injection prevention

The server strips **all messages with a `system` role** from client input before forwarding to the AI provider. The server's own system prompt is injected server-side by `SystemPromptService` and is never exposed to the client.

Additionally, `assistant` and `tool` role messages are stripped from the **first message position**. This prevents an attacker from seeding a conversation with a fabricated assistant response to manipulate the model's behavior. These roles are allowed in subsequent positions to support legitimate multi-turn tool call continuations.

Any message with a non-standard role (anything other than `user`, `assistant`, or `tool`) is forced to the `user` role.

The role stripping logic is implemented in `InputValidator.cs` (lines 71–100).

### Prompt injection detection

The `InputValidator` checks every message against a configurable blocklist of known prompt injection patterns. The default patterns include:

- "ignore previous instructions"
- "ignore all previous"
- "disregard previous"
- "you are now"
- "pretend you are"
- "act as if you are"
- "new instructions:"
- "system prompt:"
- "override instructions"
- "forget your instructions"
- "ignore your instructions"

Pattern matching is case-insensitive. When a match is detected, the request is rejected with a generic error message ("Message contains disallowed content") that does not reveal which pattern was matched. A `LogWarning` is emitted for security monitoring.

> **Extending the blocklist:** Add entries to `Security:BlockedPatterns` in `appsettings.json` or `SecurityOptions.cs` to expand coverage without code changes.

### HTML and script injection blocking

Before any content reaches the AI provider, input is scanned for HTML and script injection markers:

- `<script` tags
- `<iframe` tags
- `javascript:` protocol URIs
- `onerror=` event handlers
- `onload=` event handlers

Matching is case-insensitive and uses `Span<char>`-based checks for performance. If any injection marker is detected, the entire request is rejected.

This control prevents a stored XSS scenario where a malicious user injects script content into a conversation that could later be rendered in another user's browser.

### Client tool allowlisting

When a client declares tools (function definitions for the AI model to call), every tool name is validated against an explicit allowlist defined in `SecurityOptions.AllowedToolNames`. The default allowlist includes:

- `GetStoryGraph`
- `SearchStory`
- `SaveStoryState`
- `RollDice`
- `GetWorldRules`

Any tool not on the allowlist is rejected with a descriptive error. This prevents clients from registering arbitrary tool definitions that could manipulate the AI model's behavior.

### Tool result size limit

Tool call results returned by the client are truncated to a maximum of **32,768 characters** (32 KB). This limit is enforced in `ChatEndpoints.cs` and `PlayEndpoints.cs` and prevents a compromised client from sending oversized tool results that could exhaust server memory or model context.

## Output security

### Content filtering

All AI responses pass through `ContentFilter` before being returned to the client. The filter uses compiled regular expressions (`GeneratedRegex`) to remove:

| Pattern | Replacement |
|---|---|
| `<script>…</script>` tags | `[content removed]` |
| `<iframe>…</iframe>` tags | `[content removed]` |
| Quoted event handlers (`onerror="…"`, `onload="…"`, etc.) | Removed |
| Unquoted event handlers (`onerror=alert(1)`) | Removed |
| `javascript:` protocol URIs | Removed |

When content is modified, a `LogWarning` is emitted for security monitoring. The filter is implemented in `ContentFilter.cs`.

This control mitigates the risk of indirect prompt injection, where an attacker embeds malicious instructions in data that the AI model incorporates into its response.

### Global exception handler

The `GlobalExceptionHandler` class implements `IExceptionHandler` and ensures that no internal details — stack traces, connection strings, or framework internals — are ever leaked to clients. Every unhandled exception is mapped to a user-friendly `ProblemDetails` response:

| Exception type | HTTP status | Client message |
|---|---|---|
| `OperationCanceledException` | 499 Client Closed Request | "Request cancelled" |
| `TimeoutException` | 504 Gateway Timeout | "Request timed out" |
| `UnauthorizedAccessException` | 403 Forbidden | "Access denied" |
| All other exceptions | 500 Internal Server Error | "An internal error occurred" |

Full exception details are logged server-side at `Error` level with the request method and path for diagnostic purposes.

### ProblemDetails

The server registers `AddProblemDetails()` to ensure that all error responses — including model validation failures and status code results — follow the [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) ProblemDetails format. This provides a consistent, machine-readable error contract for clients without exposing implementation details.

## Network security

### Security headers

A custom middleware in `Program.cs` sets the following headers on every response:

| Header | Value | Purpose |
|---|---|---|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'` | Restricts resource loading to same origin; allows WASM execution |
| `X-Content-Type-Options` | `nosniff` | Prevents MIME-type sniffing |
| `X-Frame-Options` | `DENY` | Blocks framing (clickjacking defense) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limits referrer information leakage |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Disables sensitive browser APIs |

### Transport security

- **HTTPS redirection** is enforced via `app.UseHttpsRedirection()` in production environments (disabled in development for local tooling compatibility).
- **HSTS** (HTTP Strict Transport Security) is enabled via `app.UseHsts()` in non-development environments. This instructs browsers to only connect over HTTPS for future requests.

### CORS policy

Cross-Origin Resource Sharing is configured with an explicit, restrictive policy in `Program.cs`:

| Setting | Value |
|---|---|
| Allowed origin | Configured via `Client:Origin` (no wildcard) |
| Allowed headers | `Content-Type`, `Authorization`, `Accept` |
| Allowed methods | `GET`, `POST` |
| Preflight cache | 10 minutes |

The use of an explicit origin (rather than `*`) is essential because the API uses `Authorization` headers. Wildcard origins cannot be used with credentialed requests per the CORS specification.

### Request body size limit

Kestrel is configured to reject request bodies larger than **1 MB** (`1,048,576` bytes). This is set in `Program.cs` via `ConfigureKestrel` and provides a server-level defense against payload-based denial of service attacks, independent of the application-level message length limits.

## Rate limiting

The server implements per-user token bucket rate limiting to prevent individual users from monopolizing AI resources.

### Configuration

| Setting | Default | Config key |
|---|---|---|
| Token limit (requests per window) | 30 | `RateLimiting:PermitLimit` |
| Window duration | 60 seconds | `RateLimiting:WindowSeconds` |
| Queue limit | 2 | Hardcoded |
| Queue processing order | Oldest first | Hardcoded |
| Rejection status code | 429 Too Many Requests | Hardcoded |

### Partitioning

Rate limits are partitioned by the authenticated user's `ClaimTypes.NameIdentifier` claim. If the user is not authenticated (which should not occur on protected endpoints, but is handled defensively), the partition falls back to the client's IP address, with a final fallback of `"anonymous"`.

### Scope

The `"chat"` rate limiting policy is applied to:

- `/api/chat` and `/api/chat/stream` endpoints
- `/api/play` endpoints
- `/api/ping` endpoint

A separate `"auth"` rate limiting policy (10 requests/minute per IP, fixed window) is applied to Identity endpoints (`/login`, `/register`) to prevent brute-force attacks.

Session management and memory endpoints do not carry the rate limiting policy, as they do not invoke the AI provider and are lower-cost operations.

## Data security

### Conversation isolation

Every conversation session is associated with a `UserId` at creation time. Before any operation on a session — retrieving history, sending messages, or streaming responses — the endpoint calls `GetSessionOwnerAsync` to verify that the authenticated user is the session owner.

If the ownership check fails, the endpoint returns `403 Forbidden`. This prevents insecure direct object reference (IDOR) vulnerabilities where an attacker could access another user's conversations by guessing or enumerating session IDs.

The ownership check is enforced at the endpoint level in `ChatEndpoints.cs`, `SessionEndpoints.cs`, and `PlayEndpoints.cs`. The data layer (`EfConversationStore`) provides the ownership query but does not enforce it, following the principle that authorization decisions belong in the request pipeline.

### Session ID validation

Session IDs are validated for length (maximum 128 characters) at every endpoint that accepts them. This prevents excessively long identifiers from being used in database queries or logged in a way that could cause log injection.

### Memory content validation

The memory endpoints (`/api/memory`) validate content length against a 2,000-character limit and run all content through `InputValidator` to apply the same injection detection and sanitization as chat messages.

## Observability and audit

### Security audit logging

A custom middleware in `Program.cs` intercepts all responses with `401 Unauthorized` or `403 Forbidden` status codes and emits a structured `LogWarning`:

```
Security Audit: Access denied ({StatusCode}) for user {User} at {Path}
```

This provides a centralized audit trail for all authentication and authorization failures, enabling security teams to detect brute-force attacks, credential stuffing, or unauthorized access attempts.

### Tool execution logging

The `ChatEndpoints` and `PlayEndpoints` log every tool execution with structured fields:

- **Information:** Tool name, execution round, and round limit for normal tool calls
- **Warning:** Tool result truncation events (when results exceed the 32 KB limit) and tool call round limit reached
- **Error:** Tool execution failures with exception details

### Content filter event logging

The `ContentFilter` emits a `LogWarning` whenever content is removed from an AI response. Combined with the input validation logging (prompt injection pattern detection, HTML injection blocking, and role stripping), this provides end-to-end visibility into security-relevant content events.

### Health checks

An AI provider health check (`AiProviderHealthCheck`) is registered and tagged with `"ready"`. This allows orchestrators and load balancers to verify that the server can communicate with Azure OpenAI before routing traffic to the instance.

## Secret management

### Configuration precedence

The server loads configuration in the following order, where later sources override earlier ones:

1. `appsettings.json` — base configuration with non-sensitive defaults
2. `appsettings.{Environment}.json` — environment-specific overrides
3. `secrets.json` — local development secrets (gitignored, located at the repository root)
4. **Environment variables** — production secrets, re-added last to ensure they take priority

The `secrets.json` file is loaded early in `Program.cs`, and environment variables are explicitly re-added afterward using `AddEnvironmentVariables()`. This ensures that production deployments can override any local secret via environment variables without modifying files.

### Credential handling

- **No hardcoded credentials** exist in application code. AI provider keys, endpoints, and deployment names are loaded from configuration.
- The `secrets.json` file is listed in `.gitignore` and is never committed to source control.
- Seed data passwords use configuration values with fallback to randomly generated passwords when no configuration is provided.
- **Production recommendation:** Use Azure Key Vault or a managed identity to provide AI provider credentials instead of environment variables for additional protection.

## AI-specific security

### Server-controlled system prompt

The system prompt is defined server-side in configuration (`AI:SystemPrompt`) and injected by `SystemPromptService`. It is prepended to the conversation as a `system` role message before the request is forwarded to Azure OpenAI.

The client never sees the system prompt content. Combined with the input validation that strips client-supplied `system` role messages, this ensures that the model's behavioral instructions cannot be read or overridden by the client.

### Tool call round limits

To prevent runaway tool call loops, each endpoint enforces a maximum number of tool call rounds per request:

| Endpoint | Max tool call rounds |
|---|---|
| `/api/chat` | 5 |
| `/api/play` | 8 |

When the limit is reached, a `LogWarning` is emitted and the conversation continues without further tool calls. The play endpoint has a higher limit to accommodate the more complex multi-step interactions required by game engine scenarios.

### AI call timeout

Streaming AI calls are protected by a **5-minute timeout** via `CancellationTokenSource.CancelAfter`. This prevents indefinitely hanging requests from consuming server resources if the AI provider becomes unresponsive.

After the AI call completes, conversation persistence runs with a separate **5-second timeout** on a best-effort basis, ensuring that a slow database write does not block the client response.

### Tool execution sandboxing

Server-side tools (such as game engine tools) execute entirely on the server. The client declares tool schemas, but the actual tool logic runs in the server process with server-side data access. Clients can only return results for tools the AI model invokes — they cannot trigger arbitrary server-side tool execution.

Tool results from clients are validated against the tool allowlist, truncated to 32 KB, and treated as untrusted input.

### Structured output validation

AI model responses are parsed and validated before being returned to the client. The content filter ensures that even if the AI model produces unexpected output (due to indirect prompt injection or model misbehavior), dangerous content is removed before it reaches the client.

## Key files reference

| File | Purpose |
|---|---|
| `src/SecureProxyChatClients.Server/Program.cs` | Middleware pipeline, security headers, rate limiting, CORS, authentication, Kestrel limits |
| `src/SecureProxyChatClients.Server/Security/InputValidator.cs` | Input validation, role stripping, prompt injection detection, HTML injection blocking, tool allowlisting |
| `src/SecureProxyChatClients.Server/Security/ContentFilter.cs` | Output sanitization of AI responses |
| `src/SecureProxyChatClients.Server/Security/GlobalExceptionHandler.cs` | Exception-to-ProblemDetails mapping, stack trace suppression |
| `src/SecureProxyChatClients.Server/Security/SecurityOptions.cs` | Configurable security limits, blocked patterns, and tool allowlist |
| `src/SecureProxyChatClients.Server/Endpoints/ChatEndpoints.cs` | Chat endpoint auth, rate limiting, tool call limits, session ownership |
| `src/SecureProxyChatClients.Server/Endpoints/PlayEndpoints.cs` | Play endpoint auth, rate limiting, tool call limits |
| `src/SecureProxyChatClients.Server/Endpoints/SessionEndpoints.cs` | Session management auth and ownership validation |
| `src/SecureProxyChatClients.Server/Endpoints/MemoryEndpoints.cs` | Memory endpoint auth and content validation |
| `src/SecureProxyChatClients.Server/Services/SystemPromptService.cs` | Server-side system prompt injection |
| `src/SecureProxyChatClients.Server/Data/EfConversationStore.cs` | Conversation persistence and ownership queries |

## Deployment security checklist

Use this checklist when deploying SecureProxyChatClients to a production environment.

### Secrets and configuration

- [ ] AI provider API keys are provided via environment variables or Azure Key Vault — not in files
- [ ] `secrets.json` is absent from deployed artifacts and is listed in `.gitignore`
- [ ] The `Client:Origin` value in configuration matches the exact production client URL (no wildcards)
- [ ] Default seed user credentials have been removed or changed for production
- [ ] Database connection strings use least-privilege credentials

### Transport and network

- [ ] HTTPS is enforced end-to-end (terminate TLS at the load balancer or in Kestrel)
- [ ] HSTS is active (automatically enabled outside the `Development` environment)
- [ ] CORS origin is set to the production client domain only
- [ ] A reverse proxy or WAF is configured in front of the application for additional network-level protection

### Authentication

- [ ] Bearer token scheme is the only authentication path to API endpoints
- [ ] Account lockout is enabled (default: 5 attempts, 5-minute lockout)
- [ ] Bearer token storage is handled securely (in-memory, never persisted to disk/localStorage)

### Rate limiting

- [ ] `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds` are tuned for expected traffic patterns
- [ ] Rate limiting responses (429) are monitored in application logs or metrics dashboards

### Input validation

- [ ] `Security:BlockedPatterns` includes patterns relevant to your specific AI use case
- [ ] `Security:AllowedToolNames` contains only the tools your application actually uses
- [ ] Message length limits (`MaxMessages`, `MaxMessageLength`, `MaxTotalLength`) are appropriate for your use case
- [ ] Kestrel `MaxRequestBodySize` is set (default: 1 MB)

### Monitoring and incident response

- [ ] Security audit log events (401/403) are collected in a centralized logging system
- [ ] Content filter events are monitored for trends indicating indirect prompt injection attempts
- [ ] Tool execution errors are alerted on to detect potential abuse
- [ ] AI provider health check is integrated with your monitoring platform
- [ ] Log retention policies comply with your organization's requirements

### AI-specific

- [ ] System prompt content is reviewed and does not contain sensitive internal information
- [ ] Tool call round limits are appropriate for your scenarios (chat: 5, play: 8)
- [ ] AI call timeout (5 minutes) is suitable for your expected response times
- [ ] Content filter patterns cover threats specific to your domain

## Next steps

- Review `docs/ag-ui-security-considerations.md` for additional security considerations specific to the AG-UI protocol integration.
- Explore the test suite in `tests/` for security-focused test cases that validate these controls.
- Consider adding Azure Key Vault integration for production secret management.
- Evaluate adding request logging middleware for full request/response audit trails in compliance-sensitive environments.
