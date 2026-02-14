# Deployment guide

> Deploy SecureProxyChatClients — a secure AI proxy (BFF pattern) between Blazor WASM clients and Azure OpenAI.

This guide covers local development, testing, and production deployment for the SecureProxyChatClients solution. The application uses .NET Aspire for local orchestration and follows the Backend for Frontend (BFF) pattern to keep API keys and AI credentials on the server, never exposing them to browser clients.

## Architecture overview

| Component | Project | Description |
|-----------|---------|-------------|
| **Server** | `SecureProxyChatClients.Server` | ASP.NET Core API — authenticates users, proxies AI requests, enforces rate limiting |
| **Client** | `SecureProxyChatClients.Client.Web` | Blazor WebAssembly frontend — communicates only with the Server |
| **AppHost** | `SecureProxyChatClients.AppHost` | .NET Aspire orchestrator — wires up Server, Client, and PostgreSQL for local dev |
| **ServiceDefaults** | `SecureProxyChatClients.ServiceDefaults` | Shared OpenTelemetry, health checks, and resilience configuration |
| **Shared** | `SecureProxyChatClients.Shared` | Shared models and contracts |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.102 or later)
- .NET Aspire workload — install with `dotnet workload install aspire`
- **Optional:** An [Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/) resource with a `gpt-4o` deployment (required only for real AI features)
- **Optional:** PostgreSQL with the [pgvector](https://github.com/pgvector/pgvector) extension (for the vector memory store; Aspire provisions this automatically in local dev)

---

## Quick start (local development)

### 1. Clone and restore

```bash
git clone <repo-url> && cd SecureProxyChatClients
dotnet restore
```

### 2. Run with Aspire

```bash
dotnet run --project src/SecureProxyChatClients.AppHost/
```

Aspire starts the Server, Client, PostgreSQL (with pgvector), and pgAdmin. Open the **Aspire dashboard** URL printed to the console to view resource status, logs, and traces.

| Resource | Default URL |
|----------|-------------|
| Server API | `http://localhost:5167` |
| Client Web | `http://localhost:5053` |

### 3. Sign in

A seed user is created automatically in development:

| Field | Value |
|-------|-------|
| Email | `test@test.com` |
| Password | `Test123!` |

The seed user is configured in `src/SecureProxyChatClients.Server/appsettings.json` under the `SeedUser` section.

### 4. AI provider

By default, Aspire starts the server with the **Fake** AI provider so you can develop and test without an Azure OpenAI resource. To use a real provider, see [Configuring Azure OpenAI](#configuring-azure-openai).

---

## Configuration reference

Configuration follows the standard .NET precedence: `appsettings.json` → `appsettings.{Environment}.json` → `secrets.json` → environment variables (highest priority).

### AI provider settings

| Key | Environment variable | Description |
|-----|---------------------|-------------|
| `AI:Provider` | `AI__Provider` | `AzureOpenAI`, `Fake`, or `CopilotCli`. Default: `Fake` in local dev |
| `AI:Endpoint` | `AI__Endpoint` | Azure OpenAI endpoint URL |
| `AI:ApiKey` | `AI__ApiKey` | Azure OpenAI API key |
| `AI:DeploymentName` | `AI__DeploymentName` | Chat completion deployment (for example, `gpt-4o`) |
| `AI:EmbeddingDeploymentName` | `AI__EmbeddingDeploymentName` | Embedding deployment (for example, `text-embedding-3-small`) |

### Rate limiting

| Key | Environment variable | Default | Description |
|-----|---------------------|---------|-------------|
| `RateLimiting:PermitLimit` | `RateLimiting__PermitLimit` | `30` | Requests per window per user |
| `RateLimiting:WindowSeconds` | `RateLimiting__WindowSeconds` | `60` | Window duration in seconds |

Rate limiting uses a per-user token bucket partitioned by authenticated user ID (falling back to IP address for unauthenticated requests). Exceeding the limit returns `429 Too Many Requests`.

### Security

| Key | Environment variable | Default | Description |
|-----|---------------------|---------|-------------|
| `Security:MaxMessages` | `Security__MaxMessages` | — | Maximum messages per session |
| `Security:MaxMessageLength` | `Security__MaxMessageLength` | — | Maximum characters per message |

### Connection strings

| Key | Environment variable | Default | Description |
|-----|---------------------|---------|-------------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Data Source=app.db` | Primary database (SQLite default) |
| `ConnectionStrings:VectorStore` | `ConnectionStrings__VectorStore` | — | PostgreSQL with pgvector for memory |

### Client and identity

| Key | Environment variable | Description |
|-----|---------------------|-------------|
| `Client:Origin` | `Client__Origin` | Allowed CORS origin. Default: `http://localhost:5053` |
| `SeedUser:Email` | `SeedUser__Email` | Development seed user email |
| `SeedUser:Password` | `SeedUser__Password` | Development seed user password |

### Configuring Azure OpenAI

Create a `secrets.json` file at the **repository root** (this path is gitignored):

```json
{
  "AI": {
    "Provider": "AzureOpenAI",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-key",
    "DeploymentName": "gpt-4o"
  }
}
```

The server loads this file from `../../secrets.json` relative to its content root. Environment variables override values in `secrets.json`, making them the preferred mechanism in CI and production.

---

## Running with Aspire

.NET Aspire is the recommended way to run the full stack locally. The AppHost project (`src/SecureProxyChatClients.AppHost/AppHost.cs`) orchestrates:

- **PostgreSQL** with the pgvector extension and a `vectorstore` database
- **pgAdmin** for database management
- **Server** with a dependency on PostgreSQL
- **Client.Web** with a dependency on the Server

```bash
dotnet run --project src/SecureProxyChatClients.AppHost/
```

The Aspire dashboard provides real-time visibility into resource health, structured logs, distributed traces, and metrics.

### Running without Aspire

To run the Server and Client independently (for example, in an IDE):

1. Start PostgreSQL manually or use the SQLite default connection string.
2. Run the Server: `dotnet run --project src/SecureProxyChatClients.Server/`
3. Run the Client: `dotnet run --project src/SecureProxyChatClients.Client.Web/`

The Client expects the Server at `http://localhost:5167` (configured in `src/SecureProxyChatClients.Client.Web/wwwroot/appsettings.json`).

---

## Testing

The solution includes four test projects:

| Project | Type | Count | Command |
|---------|------|-------|---------|
| `Tests.Unit` | xUnit unit tests | ~258 | `dotnet test tests/SecureProxyChatClients.Tests.Unit/` |
| `Tests.Integration` | Aspire integration tests | ~7 | `dotnet test tests/SecureProxyChatClients.Tests.Integration/` |
| `Tests.Playwright` | End-to-end browser tests | — | `dotnet test tests/SecureProxyChatClients.Tests.Playwright/` |
| `Tests.Smoke` | Smoke tests | — | `dotnet test tests/SecureProxyChatClients.Tests.Smoke/` |

### Running unit tests

```bash
dotnet test tests/SecureProxyChatClients.Tests.Unit/ --configuration Release
```

Unit tests have no external dependencies and run in any environment.

### Running integration tests

Integration tests use .NET Aspire's testing infrastructure and start real resources. To run without an Azure OpenAI dependency, set the Fake provider:

```bash
AI__Provider=Fake dotnet test tests/SecureProxyChatClients.Tests.Integration/ --configuration Release
```

On CI, you may also need:

```bash
DOTNET_ASPIRE_ALLOW_UNSECURED_TRANSPORT=true
```

### Continuous integration

The project includes a GitHub Actions workflow at `.github/workflows/ci.yml`. It runs on pushes and pull requests to `main` and performs: restore → build (Release) → unit tests → integration tests (with `AI__Provider=Fake`). Test results are uploaded as artifacts in TRX format.

---

## Production deployment

### Environment configuration

In production, supply configuration through environment variables. Never deploy with `secrets.json` or default seed credentials.

**Required variables:**

```
AI__Provider=AzureOpenAI
AI__Endpoint=https://your-resource.openai.azure.com/
AI__ApiKey=<key>
AI__DeploymentName=gpt-4o
Client__Origin=https://your-production-domain.com
ConnectionStrings__DefaultConnection=<production-database-connection-string>
```

### Database

The default `Data Source=app.db` SQLite connection is intended for development only. For production, use PostgreSQL or SQL Server and supply the connection string via `ConnectionStrings__DefaultConnection`.

If you use the vector memory store, provide a PostgreSQL (pgvector) connection string via `ConnectionStrings__VectorStore`.

### HTTPS and HSTS

HSTS is enabled automatically when `ASPNETCORE_ENVIRONMENT` is not `Development`. Ensure your hosting environment terminates TLS or configure Kestrel with a valid certificate.

### CORS

Set `Client__Origin` to the exact origin of your deployed frontend. The server allows only `GET` and `POST` methods with `Content-Type`, `Authorization`, and `Accept` headers.

### Request size limits

The server enforces a 1 MB request body size limit. Adjust this in `Program.cs` if your workload requires larger payloads.

---

## Azure deployment

The following approaches are recommended for deploying to Azure. Choose the option that best fits your operational requirements.

### Option 1: Azure Container Apps (recommended)

1. Publish the Server and Client projects as container images.
2. Deploy to [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/) with environment variables for all required configuration.
3. Use [Azure Key Vault](https://learn.microsoft.com/azure/key-vault/) for secrets (`AI__ApiKey`, connection strings).
4. Use managed identity to authenticate to Azure OpenAI instead of API keys where possible.

### Option 2: Azure App Service

1. Publish each project with `dotnet publish --configuration Release`.
2. Deploy the Server to an App Service instance. Configure application settings for all environment variables.
3. Deploy the Client as a static web app or as a second App Service instance.
4. Enable App Service authentication or configure the built-in Identity system.

### Option 3: Azure Kubernetes Service (AKS)

For enterprise workloads, deploy to AKS with Helm charts or Kubernetes manifests. Use the [Azure Key Vault provider for Secrets Store CSI Driver](https://learn.microsoft.com/azure/aks/csi-secrets-store-driver) to inject secrets.

### Azure OpenAI networking

For production security, configure your Azure OpenAI resource with:

- **Private endpoints** to restrict network access
- **Managed identity** authentication instead of API keys
- **Content filtering** policies appropriate for your use case

---

## Monitoring and health checks

### Health endpoints

The ServiceDefaults project registers two health check endpoints (mapped in development only by default):

| Path | Purpose | Tags |
|------|---------|------|
| `/health` | Readiness — all checks must pass (includes AI provider connectivity) | `ready` |
| `/alive` | Liveness — basic process responsiveness | `live` |

The AI provider health check sends a test message with a 10-second timeout to verify connectivity.

> [!NOTE]
> Health check endpoints are mapped only when `ASPNETCORE_ENVIRONMENT=Development`. To expose them in production, update the health check mapping in `src/SecureProxyChatClients.ServiceDefaults/Extensions.cs`.

### OpenTelemetry

The ServiceDefaults project configures OpenTelemetry with the following instrumentation:

**Metrics:**
- `ai.prompt_tokens`, `ai.completion_tokens` — token usage counters
- `ai.request_duration` — AI request latency histogram (milliseconds)
- `ai.requests`, `ai.errors` — request and error counters
- ASP.NET Core, HttpClient, and .NET runtime metrics

**Tracing:**
- ASP.NET Core and HttpClient distributed tracing
- Health check paths (`/health`, `/alive`) are excluded from traces

**OTLP export** is enabled automatically when the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable is set. The Aspire dashboard consumes these signals in local development.

### Audit logging

The server logs `401 Unauthorized` and `403 Forbidden` responses with the request path and user identity. Review these logs for unauthorized access attempts.

---

## Troubleshooting

### Aspire fails to start

| Symptom | Resolution |
|---------|------------|
| `Aspire workload not found` | Run `dotnet workload install aspire` |
| Port conflict on 5167 or 5053 | Stop the process using the port or change ports in `launchSettings.json` |
| PostgreSQL container fails | Ensure Docker is running (Aspire provisions PostgreSQL as a container) |

### AI provider errors

| Symptom | Resolution |
|---------|------------|
| `AI provider not configured` | Verify `AI:Provider` is set. Use `Fake` for testing without Azure |
| `401` from Azure OpenAI | Verify `AI:Endpoint` and `AI:ApiKey` in `secrets.json` or environment variables |
| `404` from Azure OpenAI | Verify `AI:DeploymentName` matches a deployment in your Azure OpenAI resource |
| Health check times out | Check network connectivity to the Azure OpenAI endpoint |

### Authentication issues

| Symptom | Resolution |
|---------|------------|
| `401` on all API requests | Log in first via `POST /login` — see `docs/api.md` |
| Seed user doesn't exist | Verify `SeedUser` config in `appsettings.json`. The seed service runs at startup in development |
| Account locked out | Wait 5 minutes (lockout after 5 failed attempts) or restart the application to reset in-memory state |

### CORS errors in the browser

Verify `Client:Origin` matches the exact origin of the client (scheme, host, and port). Mismatched origins produce `403` or missing `Access-Control-Allow-Origin` headers.

---

## Security checklist for production

Use this checklist before deploying to a production environment.

- [ ] **Remove or reconfigure the seed user.** Change `SeedUser:Password` to a strong value or remove the seed user service entirely. Never deploy with the default `Test123!` password.
- [ ] **Set `AI:Provider` to `AzureOpenAI`** with proper credentials supplied via environment variables or a secrets manager — not `secrets.json`.
- [ ] **Use managed identity** for Azure OpenAI authentication instead of API keys where possible.
- [ ] **Configure `Client:Origin`** to your production frontend domain. Do not use wildcards.
- [ ] **Use a production database.** Replace the default SQLite connection with PostgreSQL or SQL Server.
- [ ] **Enable HSTS.** Set `ASPNETCORE_ENVIRONMENT=Production` — HSTS is applied automatically.
- [ ] **Tune rate limiting.** Adjust `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds` for your expected production load.
- [ ] **Set `Security:MaxMessages` and `Security:MaxMessageLength`** to prevent abuse.
- [ ] **Review security headers.** The server sets `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, and `Permissions-Policy` automatically. Verify these match your requirements.
- [ ] **Configure audit log monitoring.** Forward logs to a centralized system and alert on `401`/`403` patterns.
- [ ] **Expose health checks in production.** Update `ServiceDefaults/Extensions.cs` to map `/health` and `/alive` outside development if your infrastructure requires them.
- [ ] **Enable OTLP export.** Set `OTEL_EXPORTER_OTLP_ENDPOINT` to send metrics and traces to Azure Monitor, Prometheus, or your observability platform.
- [ ] **Consider Microsoft Entra ID** for enterprise authentication instead of the built-in Identity system.
- [ ] **Restrict Azure OpenAI network access** with private endpoints and virtual network integration.
- [ ] **Review password policy.** The default requires 8+ characters with at least one digit. Strengthen as needed for your security posture.
- [ ] **Store secrets securely.** Use Azure Key Vault, GitHub Actions secrets, or your platform's secret management — never commit credentials to source control.

---

## Related documentation

- [API reference](api.md)
- [Security considerations](ag-ui-security-considerations.md)
- [Security and deployment recommendations](recommendations.md)
- [.NET Aspire overview](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/ai-services/openai/)
