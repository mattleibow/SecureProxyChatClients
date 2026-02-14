# Authentication & Authorization — Identity, Tokens, and Session Isolation

## Overview

The secure proxy uses ASP.NET Core Identity with Bearer token authentication. This design eliminates CSRF attacks by ensuring that no authentication credential is automatically attached to requests by the browser. Every API call requires an explicit `Authorization: Bearer <token>` header.

## ASP.NET Core Identity Configuration

Identity is configured using the `AddIdentityApiEndpoints<IdentityUser>` extension, which provides ready-made `/login` and `/register` endpoints. The critical configuration overrides the default authentication scheme to Bearer-only:

- **Default authenticate scheme** — `IdentityConstants.BearerScheme`
- **Default challenge scheme** — `IdentityConstants.BearerScheme`
- **Cookie authentication** — Not configured for API routes

Entity Framework Core stores user data alongside conversation sessions in the application database. The login endpoint returns a JSON response containing `accessToken`, `refreshToken`, and `expiresIn` (3,600 seconds by default).

## Why Bearer-Only Authentication

Cookie-based authentication is vulnerable to Cross-Site Request Forgery (CSRF) because browsers automatically attach cookies to every request matching the cookie's domain. Bearer tokens are immune to CSRF because:

1. **Explicit attachment** — The client must programmatically add the `Authorization` header to each request
2. **No ambient credentials** — Browsers never automatically send Bearer tokens
3. **No CSRF tokens needed** — The authentication mechanism itself prevents the attack

This eliminates an entire class of vulnerabilities without requiring anti-forgery tokens or SameSite cookie configuration.

## Password Policy

Passwords are enforced against a policy that exceeds OWASP minimum recommendations:

| Requirement | Value |
|-------------|-------|
| Minimum length | 12 characters |
| Uppercase letter | Required |
| Lowercase letter | Required |
| Digit | Required |
| Special character | Required |

These requirements are enforced at registration time by ASP.NET Core Identity's built-in validators.

## Account Lockout

Brute-force login attempts trigger automatic account lockout:

| Setting | Value |
|---------|-------|
| Max failed attempts | 5 |
| Lockout duration | 15 minutes |

Lockout is tracked per-account and resets after the lockout window expires. Combined with the auth endpoint rate limiter (10 requests per minute per IP), this provides defense against both targeted and distributed brute-force attacks.

## Session Ownership Validation

Every session belongs to a single user. Session access follows a strict ownership model:

1. **Extract user identity** — The `ClaimTypes.NameIdentifier` claim is read from the Bearer token
2. **Query session owner** — The server retrieves the `UserId` associated with the requested session ID
3. **Compare ownership** — If the requesting user does not match the session owner, the server returns `403 Forbidden`

This validation occurs on every session-scoped operation: reading history, sending messages, accessing game state, and storing memories. There is no admin override or shared session access.

## Token Management in the Client

The Blazor WebAssembly client stores authentication tokens exclusively in memory:

- **In-memory storage** — Tokens are held in a C# variable, never written to `localStorage` or `sessionStorage`
- **Automatic attachment** — A custom `DelegatingHandler` adds the `Authorization` header to every API request
- **Token refresh** — The client uses the refresh token to obtain new access tokens before expiration
- **Logout clears state** — All in-memory tokens are discarded on logout

Avoiding browser storage protects against XSS-based token theft. If an attacker injects script into the page, they cannot access tokens from the JavaScript context because the tokens exist only in the .NET runtime's managed memory.

## Auth Endpoint Rate Limiting

Authentication endpoints use a stricter rate limiting policy than the rest of the API:

| Setting | Value |
|---------|-------|
| Strategy | Fixed window |
| Limit | 10 requests per minute |
| Partition | Source IP address |
| Queue limit | 0 (no queuing) |

This prevents credential stuffing and brute-force attacks at the network level, before request processing begins.

## Security Pipeline for Authenticated Requests

Every authenticated API request passes through the following middleware, in order:

1. **Security headers** — Response headers (CSP, X-Frame-Options, etc.) are set
2. **Audit logging** — 401 and 403 responses are logged at warn level
3. **HTTPS redirection** — HTTP requests are redirected to HTTPS
4. **CORS** — Origin validation against the configured client URL
5. **Rate limiting** — Token bucket or fixed window check
6. **Authentication** — Bearer token validation
7. **Authorization** — Endpoint-level authorization policies

This ordering ensures that rate limiting executes before authentication, preventing unauthenticated requests from consuming authentication resources.

## Next Steps

- [AI Integration](05-ai-integration.md) — Azure OpenAI setup and tool calling
- [API Reference](06-api-reference.md) — Complete endpoint documentation
