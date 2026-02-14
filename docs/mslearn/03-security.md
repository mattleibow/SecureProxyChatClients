# Security — Defense-in-Depth Controls

## Threat Model

The secure AI proxy addresses threats across three attack surfaces:

### Client-to-Server Attacks
- **Prompt injection** — Malicious prompts that attempt to override system instructions
- **Cross-site request forgery (CSRF)** — Exploiting ambient authentication credentials
- **Input manipulation** — Oversized messages, role spoofing, tool abuse
- **Session hijacking** — Accessing other users' conversations

### Server-to-AI Attacks
- **Token exhaustion** — Consuming excessive AI tokens via rapid requests
- **System prompt extraction** — Tricking the AI into revealing its instructions

### AI-to-Client Attacks
- **Cross-site scripting (XSS)** — AI generating responses with executable code
- **Data exfiltration** — AI responses containing sensitive server information

## Security Controls

The implementation includes 20 named security controls:

### S1 — Bearer Token Authentication

API endpoints exclusively accept Bearer tokens via the `Authorization` header. Cookie-based authentication is disabled for API routes, eliminating CSRF as an attack vector. Browsers do not automatically attach Bearer tokens to cross-origin requests.

### S2 — Input Validation

All incoming messages are validated against configurable limits:
- Maximum message count per request (default: 10)
- Maximum characters per message (default: 4,000)
- Maximum total characters per request (default: 50,000)
- Role enforcement — client messages forced to `user` role
- System role stripping — clients cannot inject system prompts

### S3 — Prompt Injection Detection

A configurable blocklist detects common jailbreak patterns: "ignore previous instructions," "you are now," "system override," etc. Matched requests are rejected with 400 Bad Request.

### S4 — Content Filtering

AI output is sanitized before reaching the client:
- Script tags (`<script>`) → removed
- Iframe tags (`<iframe>`) → removed
- Object/Embed tags (`<object>`, `<embed>`) → removed
- Event handlers (`onclick`, `onerror`, etc.) → removed
- JavaScript protocol (`javascript:`) → removed
- Double-filtering on streams (per-chunk + final concatenated text)

### S5 — Per-User Rate Limiting

Token bucket rate limiter partitioned by authenticated user ID (with IP fallback):
- Chat/play endpoints: 30 requests per 60 seconds (configurable)
- Auth endpoints: 10 requests per minute per IP (fixed window)
- Rate limiter executes before authentication to prevent resource exhaustion

### S6 — Session Ownership Validation

Every session access verifies that the requesting user owns the session. Unauthorized access returns 403 Forbidden. This prevents horizontal privilege escalation.

### S7 — Tool Allowlisting

Only pre-approved tool names are accepted from the client. Unrecognized tool names are rejected. Server-side tools execute with validated, clamped parameters.

### S8 — Tool Call Limits

AI tool execution loops are bounded (5 rounds for chat, 8 for play). This prevents infinite loops from consuming server resources.

### S9 — Tool Result Size Limits

Client-provided tool results are truncated to 32 KB maximum, preventing memory exhaustion from oversized payloads.

### S10 — Request Body Size Limit

Kestrel is configured with a 1 MB maximum request body size, providing a transport-level defense against oversized payloads.

### S11 — Security Headers

Every response includes security headers:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Content-Security-Policy: default-src 'self'; ...`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), ...`
- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Embedder-Policy: require-corp`

### S12 — CORS Restriction

CORS is configured with explicit origin, restricted methods (GET, POST, OPTIONS), and specific allowed headers. Wildcard origins are never used.

### S13 — AI Call Timeouts

All AI provider calls have explicit timeouts (5 minutes for chat/play, 2 minutes for oracle) via `CancellationTokenSource`. Hanging upstream calls do not stall the server.

### S14 — Global Exception Handler

A custom `IExceptionHandler` catches all unhandled exceptions and returns RFC 9457 ProblemDetails responses without leaking stack traces or internal details.

### S15 — Account Lockout

After 5 failed login attempts, the account is locked for 15 minutes. This protects against brute-force credential attacks.

### S16 — Password Policy

Passwords require minimum 12 characters with uppercase, lowercase, digit, and special character. This exceeds OWASP minimum recommendations.

### S17 — ForwardedHeaders Security

`KnownNetworks` and `KnownProxies` are cleared, preventing IP spoofing via untrusted `X-Forwarded-For` headers. Production deployments must explicitly configure trusted proxy networks.

### S18 — Game Input Validation

All game tool parameters are clamped to safe ranges (health ±200, gold ±1,000, XP 0–5,000, strings 500 chars). Enum values are validated against strict allowlists. Character class selection uses an allowlist to prevent prompt injection.

### S19 — Seed Data Gating

Test seed data only runs in Development environments or when explicitly enabled via `SeedUser:Enabled` configuration. Production deployments never create default accounts.

### S20 — Observability

OpenTelemetry integration provides distributed tracing, metrics, and structured logging. Security-relevant events (401/403 responses, content filter activations, rate limit hits) are logged for audit purposes.

## Deployment Security Checklist

Before deploying to production, verify:

- [ ] AI API keys are stored in Azure Key Vault or environment variables
- [ ] HTTPS is enforced with valid TLS certificates
- [ ] CORS origin matches the production client URL exactly
- [ ] Rate limiting is configured for expected traffic patterns
- [ ] ForwardedHeaders KnownProxies are configured for your load balancer
- [ ] Health check endpoints are properly secured
- [ ] Seed data is disabled (`SeedUser:Enabled` is absent or `false`)
- [ ] Logging is configured to a persistent store (Application Insights, etc.)
- [ ] Database connection strings use managed identity where possible
- [ ] Bearer token storage is handled securely (in-memory, never persisted)

## Next Steps

- [Authentication & Authorization](04-authentication.md) — Detailed auth implementation
- [AI Integration](05-ai-integration.md) — Azure OpenAI setup and tool calling
