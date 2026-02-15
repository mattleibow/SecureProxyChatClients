# Deployment — Configuration, Infrastructure, and Monitoring

## Prerequisites

- .NET 10 SDK
- Azure OpenAI resource with a deployed model (e.g., `gpt-4o`)
- Docker Desktop (for PostgreSQL via Aspire in development)
- Azure subscription (for cloud deployment)

## Configuration Reference

Configuration is loaded in the following order (highest priority wins):

1. Environment variables (using `__` as the section separator)
2. `secrets.json` (repository root, gitignored)
3. `appsettings.{Environment}.json`
4. `appsettings.json`

### AI Provider

| Key | Environment Variable | Default | Required |
|-----|---------------------|---------|----------|
| `AI:Provider` | `AI__Provider` | `Fake` | No |
| `AI:Endpoint` | `AI__Endpoint` | — | Yes (AzureOpenAI) |
| `AI:ApiKey` | `AI__ApiKey` | — | Yes (AzureOpenAI) |
| `AI:DeploymentName` | `AI__DeploymentName` | `gpt-4o` | No |
| `AI:EmbeddingDeploymentName` | `AI__EmbeddingDeploymentName` | `text-embedding-3-small` | No |
| `AI:CopilotCli:Model` | `AI__CopilotCli__Model` | `gpt-5-mini` | No |

### Security

| Key | Environment Variable | Default |
|-----|---------------------|---------|
| `Security:MaxMessages` | `Security__MaxMessages` | 50 |
| `Security:MaxMessageLength` | `Security__MaxMessageLength` | 4,000 |
| `Security:MaxTotalLength` | `Security__MaxTotalLength` | 50,000 |

### Rate Limiting

| Key | Environment Variable | Default |
|-----|---------------------|---------|
| `RateLimiting:PermitLimit` | `RateLimiting__PermitLimit` | 30 |
| `RateLimiting:WindowSeconds` | `RateLimiting__WindowSeconds` | 60 |

### Database Connections

| Key | Environment Variable | Default |
|-----|---------------------|---------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Data Source=app.db` |
| `ConnectionStrings:VectorStore` | `ConnectionStrings__VectorStore` | — |

### Seed User

| Key | Environment Variable | Default |
|-----|---------------------|---------|
| `SeedUser:Email` | `SeedUser__Email` | `test@test.com` |
| `SeedUser:Password` | `SeedUser__Password` | `TestPassword1!` |
| `SeedUser:Enabled` | `SeedUser__Enabled` | `false` (production) |

### Client and CORS

| Key | Environment Variable | Default |
|-----|---------------------|---------|
| `Client:Origin` | `Client__Origin` | `http://localhost:5053` |

## Local Development

For local development, .NET Aspire orchestrates all services:

```bash
cp secrets.json.template secrets.json
# Edit secrets.json with your Azure OpenAI credentials
dotnet run --project src/SecureProxyChatClients.AppHost
```

The AppHost provisions PostgreSQL and pgAdmin automatically via Docker. The server runs on port 5167 and the Blazor WASM client on port 5053.

To run without Azure OpenAI, use the Fake provider:

```bash
AI__Provider=Fake dotnet run --project src/SecureProxyChatClients.AppHost
```

## Azure App Service Deployment

### Publish the Server

```bash
dotnet publish src/SecureProxyChatClients.Server -c Release -o ./publish
```

Deploy the `publish` folder to Azure App Service. Configure application settings in the Azure Portal or via CLI:

```bash
az webapp config appsettings set --name <app-name> --resource-group <rg> --settings \
  AI__Provider=AzureOpenAI \
  AI__Endpoint=https://<resource>.openai.azure.com/ \
  AI__ApiKey=<key> \
  AI__DeploymentName=gpt-4o \
  Client__Origin=https://<client-url> \
  ASPNETCORE_ENVIRONMENT=Production
```

### Deploy the Client

The Blazor WebAssembly client is a static site. Publish and deploy to Azure Static Web Apps or any static hosting provider:

```bash
dotnet publish src/SecureProxyChatClients.Client.Web -c Release -o ./publish-client
```

Update the client's `appsettings.json` to point `ServerUrl` at the deployed server URL.

## Container Deployment

Build and deploy using the .NET SDK container publishing:

```bash
dotnet publish src/SecureProxyChatClients.Server -c Release /t:PublishContainer
```

Pass configuration via environment variables:

```bash
docker run -p 8080:8080 \
  -e AI__Provider=AzureOpenAI \
  -e AI__Endpoint=https://<resource>.openai.azure.com/ \
  -e AI__ApiKey=<key> \
  -e ConnectionStrings__DefaultConnection="Host=<db>;Database=app;..." \
  -e Client__Origin=https://<client-url> \
  <image-name>
```

For Azure Container Apps, configure secrets using Azure Key Vault references.

## Production Security Checklist

Before deploying to production, verify:

- [ ] `AI:Provider` is set to `AzureOpenAI`
- [ ] AI credentials are stored in Azure Key Vault or environment variables, never in source
- [ ] `SeedUser:Enabled` is absent or `false`
- [ ] `Client:Origin` matches the exact production client URL (no wildcards)
- [ ] `ASPNETCORE_ENVIRONMENT` is set to `Production` (enables HSTS)
- [ ] `ConnectionStrings:DefaultConnection` points to a production database
- [ ] ForwardedHeaders `KnownProxies` are configured for your load balancer
- [ ] Bearer token storage in the client is in-memory only
- [ ] Rate limiting values are appropriate for expected traffic

## Monitoring and Observability

### OpenTelemetry

The server integrates OpenTelemetry for distributed tracing, metrics, and structured logging. Configure the exporter endpoint:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://<collector>:4317
```

Key metrics emitted:

- AI request latency and token counts (via `ObservabilityChatClient`)
- Rate limit hits
- Authentication failures (401/403 responses)
- Content filter activations

### Health Checks

The server exposes health check endpoints for load balancer and container orchestrator integration. The AI provider health check verifies connectivity with a 10-second timeout.

### Audit Logging

Security-relevant events are logged at warn level:

- 401 Unauthorized responses (missing or invalid tokens)
- 403 Forbidden responses (session ownership violations)
- Rate limit rejections (429 responses)
- Content filter activations (XSS patterns detected in AI output)

Configure a persistent log sink (Application Insights, Seq, ELK) for production environments.

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|-------------|------------|
| 401 on all API calls | Bearer token expired or missing | Check token refresh logic; verify `Authorization` header |
| 403 on session access | Session belongs to different user | Verify session ID matches authenticated user |
| 429 on chat endpoints | Rate limit exceeded | Increase `RateLimiting:PermitLimit` or reduce request frequency |
| AI responses empty | Wrong provider or missing credentials | Verify `AI:Provider`, `AI:Endpoint`, and `AI:ApiKey` |
| CORS errors in browser | Client origin mismatch | Set `Client:Origin` to exact client URL |
| Health check failing | AI provider unreachable | Verify `AI:Endpoint` connectivity and API key validity |
| WASM client won't load | CSP blocking resources | Verify `Content-Security-Policy` allows `wasm-unsafe-eval` |
| Database errors | Connection string misconfigured | Check `ConnectionStrings:DefaultConnection` format |

## Next Steps

- [Extending](09-extending.md) — Adding new tools, providers, and security policies
- [Security](03-security.md) — Review the complete security control inventory
