# Extending — Tools, Providers, and Security Policies

## Overview

The reference implementation is designed for extension. This article covers the common extension points: adding tools, registering custom AI providers, extending security policies, and adding game mechanics.

## Adding Server-Side Tools

Server tools execute on the server during the AI tool calling loop. The AI model sees their descriptions and can invoke them without client involvement.

### Game Tools

Game tools are defined as static methods in `GameTools.cs` and registered in `GameToolRegistry.cs`.

To add a new game tool:

1. **Define the method** — Add a static method with `[Description]` attributes on the method and each parameter
2. **Define a result record** — Create a sealed record type for the tool's return value
3. **Register the tool** — Add `AIFunctionFactory.Create(GameTools.YourTool)` to the registry's tool list
4. **Handle state mutation** — Add a case to `ApplyToolResult` if the tool modifies player state (health, gold, XP, inventory)

Parameters are automatically extracted from method signatures by `AIFunctionFactory`. Use `[Description]` attributes to provide natural language descriptions that the AI model reads.

### Narrative Tools

Narrative tools follow the same pattern but live in the `Tools/` directory with individual files per tool. Registration uses reflection:

```
AIFunctionFactory.Create(typeof(YourTool).GetMethod(nameof(YourTool.Execute))!)
```

Add the new tool to `ServerToolRegistry.cs` to make it available to the AI.

## Adding Client-Side Tools

Client tools execute in the Blazor WebAssembly browser context. They are useful for operations that need local state, browser APIs, or user interaction.

To add a new client tool:

1. **Create a tool class** — Add a class in `Client.Web/Tools/` with a public method annotated with `[Description]`
2. **Register in DI** — Add the tool class to the client's service collection in `Program.cs`
3. **Register in the tool registry** — Inject the tool into `ClientToolRegistry` and add it to the function list
4. **Allowlist the tool name** — Add the tool's method name to `SecurityOptions.AllowedToolNames` in the server configuration

The allowlist step is critical. The server rejects any tool name not in the allowlist, so new client tools must be added to both the client registry and the server's security configuration.

## Custom AI Providers

The `IChatClient` interface from Microsoft.Extensions.AI defines the contract for AI providers. To add a custom provider:

1. **Implement `IChatClient`** — Create a class that implements `CompleteAsync` and `CompleteStreamingAsync`
2. **Add a configuration case** — Add a new case to the provider switch in `AiServiceExtensions.cs`
3. **Wrap with observability** — Use `new ObservabilityChatClient(yourClient)` to maintain metrics

The provider is selected by the `AI:Provider` configuration key. Any string value can be used — add a matching case to the switch statement.

Example provider candidates:

- **Ollama** — Local model execution
- **Amazon Bedrock** — AWS-hosted models
- **Custom inference** — Self-hosted model endpoints

All providers are automatically wrapped with the `ObservabilityChatClient` decorator, which adds OpenTelemetry metrics without additional code.

## Custom Security Policies

### Input Validation

The `InputValidator` class enforces security controls on incoming requests. Extend it by:

- **Adding blocked patterns** — Add strings to `SecurityOptions.BlockedPatterns` via configuration
- **Adjusting limits** — Modify `MaxMessages`, `MaxMessageLength`, or `MaxTotalLength`
- **Adding validation methods** — Add new validation checks to the validator pipeline

All configuration is accessible via `appsettings.json` or environment variables:

```json
{
  "Security": {
    "MaxMessages": 20,
    "BlockedPatterns": ["custom pattern 1", "custom pattern 2"],
    "AllowedToolNames": ["ExistingTool", "YourNewTool"]
  }
}
```

### Content Filtering

The `ContentFilter` class uses compiled regular expressions (via `[GeneratedRegex]`) to sanitize AI output. Add new filter patterns by:

1. Adding a new `[GeneratedRegex]` property for compile-time pattern generation
2. Adding the pattern to the filter pipeline

Content filtering is applied to both streaming chunks and final concatenated text.

### Rate Limiting

Rate limiting policies are defined in `Program.cs`. Add new policies by:

1. Defining a new `AddPolicy` with a policy name
2. Applying the policy to specific endpoint groups via `RequireRateLimiting`

## Adding Game Mechanics

The game engine is organized into self-contained systems, each extending a specific aspect of gameplay.

### Creatures

Add creatures to `Bestiary.cs` by adding new `Creature` records to the static list. Each creature has level, health, attack difficulty, damage, abilities, weakness, XP reward, and gold drop. The `GetCreaturesForLevel` method automatically includes them in level-appropriate encounters.

### Locations

Add locations to `WorldMap.cs` by adding new location records with coordinates and connection lists. The ASCII map renderer and `GetConnections` method handle new locations automatically.

### Achievements

Add achievements to `Achievements.cs`:

1. Define a new `Achievement` record with ID, title, description, emoji, and category
2. Add trigger logic to `CheckAchievements` to detect when the achievement should unlock

Achievement categories include combat, exploration, social, wealth, and progression.

### Twists of Fate

Add random narrative events to `TwistOfFate.cs` by adding new `Twist` records to the array. Events are categorized as environment, combat, encounter, discovery, or personal. The `GetRandomTwist` and `GetTwistByCategory` methods include new entries automatically.

## Vector Store Providers

The semantic memory system uses `IStoryMemoryService` as its abstraction:

| Method | Purpose |
|--------|---------|
| `StoreMemoryAsync` | Persist a memory entry with optional embedding |
| `SearchAsync` | Find memories by vector similarity |
| `GetRecentMemoriesAsync` | Retrieve recent memories by timestamp |

Two implementations are included:

- **PgVectorStoryMemoryService** — PostgreSQL with pgvector extension (cosine distance)
- **InMemoryStoryMemoryService** — Development fallback without vector search

To add a custom vector store (Qdrant, Pinecone, Azure AI Search), implement `IStoryMemoryService` and register it in the DI container.

## Multi-Tenant Considerations

The current implementation is single-tenant per deployment. User isolation is enforced via `UserId` fields on player state, sessions, and memories.

To add multi-tenancy:

1. **Add a tenant identifier** — Introduce a `TenantId` field to `PlayerState`, `StoryMemory`, and conversation entities
2. **Create a tenant resolver** — Build an `ITenantResolver` service that extracts tenant identity from the request context (subdomain, header, or claim)
3. **Update data access** — Filter all queries by `TenantId` in addition to `UserId`
4. **Scope configuration** — Consider per-tenant AI provider configuration and rate limiting

## Next Steps

- [Overview](01-overview.md) — Return to the introduction
- [Security](03-security.md) — Review the security control inventory
