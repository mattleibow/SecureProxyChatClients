# SecureProxyChatClients

> A reference implementation of a **secure augmenting proxy** (BFF pattern) for AI chat — built with .NET 10, Aspire, and Microsoft.Extensions.AI.

**LoreEngine** — *Your AI writing team. Your story. Your rules.*

---

## What Is This?

MAUI (and any client) apps cannot safely embed AI provider credentials or trust client-provided messages. This project demonstrates a **secure augmenting proxy** that mediates between untrusted clients and Azure OpenAI. The server doesn't just forward requests — it authenticates users, enforces security policies, executes server-side tools, filters content, and enriches/augments client requests before forwarding to AI.

The `IChatClient` abstraction from **Microsoft.Extensions.AI** is preserved on both sides of the trust boundary.

The infrastructure is showcased through **LoreEngine**, a simplified interactive fiction builder where 3 AI agents (Storyteller, Critic, Archivist) collaborate in a "Writer's Room" to build stories.

---

## Architecture

```
Blazor WASM (Client)                  ASP.NET Core (Server / BFF)
┌──────────────────────────┐  CORS   ┌─────────────────────────┐
│                          │◄───────►│                         │
│ CreateStory.razor        │         │ POST /api/chat          │
│ WritersRoom.razor        │         │ POST /api/chat/stream   │
│ Chat.razor               │         │ POST /api/sessions      │
│                          │         │ GET  /api/sessions      │
│ Agents (client-side):    │         │ GET  /api/sessions/{id} │
│ ├─ 📖 Storyteller        │         │                         │
│ ├─ 🎭 Critic             │ HTTP+   │ Security Pipeline:      │
│ └─ 📚 Archivist          │ Bearer  │ ├─ Input Validation     │
│                          │ Token   │ ├─ Role Stripping (S1)  │
│ ProxyChatClient ─────────┼────────►│ ├─ Content Filtering    │
│   (IChatClient)          │         │ ├─ Tool Allowlisting    │
│                          │         │ └─ Rate Limiting        │
│ Client Tools (local):    │         │                         │
│ ├─ GetStoryGraph         │         │ Server Tools:           │
│ ├─ SearchStory           │         │ ├─ GenerateScene        │
│ ├─ SaveStoryState        │         │ ├─ CreateCharacter      │
│ ├─ RollDice              │         │ ├─ AnalyzeStory         │
│ └─ GetWorldRules         │         │ └─ SuggestTwist         │
│                          │         │                         │
│ StoryStateService        │         │ IChatClient ────────────┼──► Azure OpenAI
│   (in-memory)            │         │   (real or fake)        │
└──────────────────────────┘         └─────────────────────────┘
         │                                      │
         └──────── .NET Aspire AppHost ─────────┘
                   (single F5 launch)
```

**Key design choice**: Agents live on the **client** (Blazor WASM). Each agent's `LoreAgent` calls `ProxyChatClient` → server → Azure OpenAI. The server is a stateless secure proxy with no game logic.

---

## Prerequisites

- **.NET 10 SDK** (LTS) — [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **.NET Aspire workload** — Install with:
  ```bash
  dotnet workload install aspire
  ```

---

## Quick Start

1. **Clone & build**
   ```bash
   git clone <repo-url>
   cd SecureProxyChatClients
   dotnet build
   ```

2. **Run with Aspire** (launches both server + client)
   ```bash
   dotnet run --project src/SecureProxyChatClients.AppHost
   ```
   The Aspire dashboard opens automatically. From there, access the client and server endpoints.

3. **Login** — In development, a seed user is created automatically:
   - **Email**: `test@test.com`
   - **Password**: `TestPassword1!`
   
   Or register a new account via the Register page (password requires 12+ chars, uppercase, lowercase, digit, and special character).

4. **Try it out**:
   - **Play** → Create a character and explore an AI-driven RPG world
   - **Create Story** → Full guided creation flow (genre → rules → pitch → scenes)
   - **Writer's Room** → Direct multi-agent discussion
   - **Chat** → Direct AI chat with streaming + tool calling

5. **Configure AI provider** (optional):
   Create a `secrets.json` file in the repository root:
   ```json
   {
     "AI": {
       "Provider": "AzureOpenAI",
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ApiKey": "YOUR-KEY",
       "DeploymentName": "gpt-4o"
     }
   }
   ```
   Without this, the app uses a built-in Fake provider for local testing.

---

## Project Structure

```
SecureProxyChatClients/
├── README.md
├── docs/
│   ├── plan.md                 ← Requirements & architecture spec
│   ├── lore-engine.md          ← Game design document
│   └── api.md                  ← API endpoint documentation
├── src/
│   ├── SecureProxyChatClients.AppHost/       ← Aspire orchestrator
│   ├── SecureProxyChatClients.ServiceDefaults/ ← Shared Aspire defaults
│   ├── SecureProxyChatClients.Server/        ← ASP.NET Core web app
│   │   ├── Endpoints/          ← Chat + Session API endpoints
│   │   ├── Security/           ← Input validation, content filtering
│   │   ├── Tools/              ← Server-side AIFunctions
│   │   ├── AI/                 ← AI provider configuration
│   │   └── Services/           ← System prompt, conversation store
│   ├── SecureProxyChatClients.Client.Web/    ← Blazor WASM app
│   │   ├── Pages/              ← Home, Login, Chat, WritersRoom, CreateStory
│   │   ├── Agents/             ← LoreAgent, WritersRoom orchestration
│   │   ├── Tools/              ← Client-side AIFunctions
│   │   └── Services/           ← ProxyChatClient, AuthState, StoryState
│   └── SecureProxyChatClients.Shared/        ← Shared DTOs & contracts
├── tests/
│   ├── Tests.Unit/             ← Fast unit tests (256+ tests)
│   ├── Tests.Integration/      ← Aspire integration tests
│   ├── Tests.Playwright/       ← Browser E2E tests
│   └── Tests.Smoke/            ← Real AI provider tests
```

---

## Testing

### Unit Tests (fastest, no server required)
```bash
dotnet test tests/SecureProxyChatClients.Tests.Unit
```

### Integration Tests (starts full Aspire app)
```bash
dotnet test tests/SecureProxyChatClients.Tests.Integration
```

### All Tests
```bash
dotnet test
```

> **Note**: Smoke tests (`Tests.Smoke`) require a real Azure OpenAI endpoint configured. Integration tests and Playwright tests start the full Aspire app. Unit tests use `FakeChatClient` for deterministic testing.

---

## Security Model

The server implements a comprehensive defense-in-depth security pipeline:

| # | Control | Description |
|---|---------|-------------|
| S1 | **Role stripping** | Forces all user-authored prompt messages to `role: user` — prevents system message injection |
| S2 | **Input validation** | Message length limits (4000 chars/message, 50000 total), HTML/script injection detection |
| S3 | **Rate limiting** | Token bucket rate limiting with burst handling (30 tokens/60s) |
| S4 | **Content filtering** | Sanitizes LLM output — removes scripts, iframes, event handlers, javascript: protocol |
| S5 | **Tool allowlisting** | Only pre-approved tool names accepted from client |
| S6 | **Prompt injection detection** | Blocked patterns for common injection attacks |
| S7 | **Session security** | Server-generated session IDs with ownership verification (IDOR prevention) |
| S8 | **Authentication** | ASP.NET Core Identity with bearer token auth, account lockout |
| S9 | **Security headers** | CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy |
| S10 | **Error handling** | Global exception handler with ProblemDetails — never leaks internal details |
| S11 | **Audit logging** | Security events (401/403) logged with structured data |
| S12 | **Request limits** | 1MB request body limit, 5-minute AI call timeout |
| S13 | **Observability** | AI metrics (token usage, latency, error rate) via OpenTelemetry |
| S14 | **Health checks** | AI provider health check, Aspire default health endpoints |
| S15 | **Concurrency control** | Optimistic locking on game state with version tracking |
| S16 | **Bearer-only API auth** | API endpoints require Bearer tokens, no cookie auth (prevents CSRF) |
| S17 | **Password policy** | 12+ chars, digit/uppercase/lowercase/special required, 15-minute lockout |
| S18 | **Auth rate limiting** | 10 requests/minute per IP on login/register endpoints |
| S19 | **ForwardedHeaders security** | KnownNetworks/KnownProxies cleared, explicit proxy config required |
| S20 | **CharacterClass allowlist** | User-supplied class validated against strict allowlist before prompt injection |

---

## Configuration

Configuration is via `appsettings.json` on the server:

### AI Provider

| Setting | Values | Description |
|---------|--------|-------------|
| `AI:Provider` | `Fake`, `CopilotCli`, `AzureOpenAI` | Which AI backend to use |
| `AI:Endpoint` | URL | Azure OpenAI endpoint (required for `AzureOpenAI`) |
| `AI:ApiKey` | string | Azure OpenAI API key (required for `AzureOpenAI`) |
| `AI:DeploymentName` | string | Model deployment name (default: `gpt-4o`) |
| `AI:CopilotCli:Model` | string | Model for Copilot CLI provider (default: `gpt-5-mini`) |
| `AI:SystemPrompt` | string | Custom system prompt (optional) |

### Security

| Setting | Default | Description |
|---------|---------|-------------|
| `Security:MaxMessages` | `10` | Max messages per request |
| `Security:MaxMessageLength` | `4000` | Max chars per message |
| `Security:MaxTotalLength` | `50000` | Max total chars per request |
| `Security:AllowedToolNames` | `[...]` | Client tool allowlist |
| `Security:BlockedPatterns` | `[...]` | Prompt injection patterns |

### Rate Limiting

| Setting | Default | Description |
|---------|---------|-------------|
| `RateLimiting:PermitLimit` | `30` | Token bucket capacity |
| `RateLimiting:WindowSeconds` | `60` | Replenishment window |

### Seed User (Development Only)

| Setting | Default | Description |
|---------|---------|-------------|
| `SeedUser:Email` | `test@test.com` | Dev user email |
| `SeedUser:Password` | *(random)* | Dev user password (set in `appsettings.Development.json`) |
| `SeedUser:Enabled` | `false` | Must be `true` for seeding in non-Development environments |

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Make your changes with tests
4. Run `dotnet build && dotnet test tests/SecureProxyChatClients.Tests.Unit` to verify
5. Submit a pull request

### Code Style
- C# 14 with file-scoped namespaces
- Minimal APIs for endpoints
- `record` types for DTOs
- `sealed` on non-inherited classes

---

## License

This is a reference sample. See the repository license for details.
