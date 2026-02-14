# Secure AI Proxy Reference Implementation — Documentation Index

> A comprehensive guide to building secure AI proxy servers for untrusted client applications using .NET 10, ASP.NET Core, and Microsoft.Extensions.AI.

## Articles

| # | Article | Description |
|---|---------|-------------|
| 1 | [Overview](01-overview.md) | Introduction, goals, target audience, and what you'll learn |
| 2 | [Architecture](02-architecture.md) | System design, trust boundaries, component responsibilities |
| 3 | [Security](03-security.md) | Defense-in-depth security controls, threat model, mitigations |
| 4 | [Authentication & Authorization](04-authentication.md) | Identity setup, Bearer tokens, session isolation |
| 5 | [AI Integration](05-ai-integration.md) | Azure OpenAI setup, tool calling, streaming, content filtering |
| 6 | [API Reference](06-api-reference.md) | Complete endpoint documentation with request/response formats |
| 7 | [Testing](07-testing.md) | Test strategy, unit/integration/E2E testing patterns |
| 8 | [Deployment](08-deployment.md) | Production deployment, configuration, monitoring |
| 9 | [Extending](09-extending.md) | Adding tools, agents, security policies, custom providers |

## Prerequisites

- .NET 10 SDK
- Azure OpenAI resource (or use the built-in Fake provider for development)
- Docker Desktop (for PostgreSQL via Aspire)
- Visual Studio 2022 17.14+ or VS Code with C# Dev Kit

## Quick Start

```bash
git clone https://github.com/mattleibow/SecureProxyChatClients.git
cd SecureProxyChatClients
cp secrets.json.template secrets.json
# Edit secrets.json with your Azure OpenAI credentials
dotnet run --project src/SecureProxyChatClients.AppHost
```

## License

This project is licensed under the MIT License. See [LICENSE](../LICENSE) for details.
