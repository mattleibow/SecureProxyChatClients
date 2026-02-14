# Extending the reference sample

This guide explains how to extend and customize the SecureProxyChatClients reference sample. Each section covers a specific extension point, references the actual source files, and shows the patterns to follow.

## Adding server tools

Server tools are static methods that the AI model calls during a conversation. They run on the server and the model never sees their source code — only the metadata generated from `[Description]` attributes.

### The AIFunction pattern

All game tools live in `src/SecureProxyChatClients.Server/GameEngine/GameTools.cs`. Each tool is a `static` method decorated with `System.ComponentModel.Description` attributes on the method and its parameters:

```csharp
[Description("Roll dice for an action check. Returns the result and whether it succeeds against a difficulty.")]
public static DiceCheckResult RollCheck(
    [Description("The stat being tested: strength, dexterity, wisdom, or charisma")] string stat,
    [Description("Difficulty class (1-20, where 10 is moderate)")] int difficulty,
    [Description("Brief description of what the player is attempting")] string action)
{
    // Implementation ...
    return new DiceCheckResult(Roll: d20, Modifier: modifier, Total: total, ...);
}
```

The `[Description]` text is what the AI model sees when deciding which tool to call. Return a sealed record so the result is strongly typed:

```csharp
public sealed record DiceCheckResult(
    int Roll, int Modifier, int Total, int Difficulty,
    bool Success, bool CriticalSuccess, bool CriticalFailure,
    string Action, string Stat);
```

The existing game tools follow this pattern:

| Tool | Purpose | Returns |
|------|---------|---------|
| `RollCheck` | Dice roll with stat modifier | `DiceCheckResult` |
| `MovePlayer` | Change player location | `LocationResult` |
| `GiveItem` | Add item to inventory | `ItemResult` |
| `TakeItem` | Remove item from inventory | `ItemResult` |
| `ModifyHealth` | Deal damage or heal | `HealthResult` |
| `ModifyGold` | Award or spend gold | `GoldResult` |
| `AwardExperience` | Award XP with level-up | `ExperienceResult` |
| `GenerateNpc` | Create NPC with hidden secrets | `NpcResult` |

### Register in GameToolRegistry

After creating a new tool method, register it in `src/SecureProxyChatClients.Server/GameEngine/GameToolRegistry.cs`:

```csharp
var tools = new List<AIFunction>
{
    AIFunctionFactory.Create(GameTools.RollCheck),
    AIFunctionFactory.Create(GameTools.MovePlayer),
    // ... existing tools
    AIFunctionFactory.Create(GameTools.YourNewTool),   // Add here
};
```

If your tool modifies player state, add a case to `ApplyToolResult`:

```csharp
public static object? ApplyToolResult(object? result, PlayerState state)
{
    switch (result)
    {
        case YourNewResult custom:
            // Mutate state as needed
            return result;
        // ... existing cases
    }
}
```

### Server-side narrative tools

For tools that the server proxy uses (not game engine tools), add them to `src/SecureProxyChatClients.Server/Tools/`. Each tool is its own class with a static method. Register it in `ServerToolRegistry`:

```csharp
// ServerToolRegistry.cs
private readonly IReadOnlyList<AIFunction> _tools =
[
    AIFunctionFactory.Create(typeof(GenerateSceneTool).GetMethod(nameof(GenerateSceneTool.GenerateScene))!, target: null, options: null),
    AIFunctionFactory.Create(typeof(YourNewTool).GetMethod(nameof(YourNewTool.Execute))!, target: null, options: null),
];
```

The four existing server tools are: `GenerateSceneTool`, `CreateCharacterTool`, `AnalyzeStoryTool`, and `SuggestTwistTool` — all in the `src/SecureProxyChatClients.Server/Tools/` directory.

## Adding client tools

Client tools run in the browser (Blazor WebAssembly) and give the AI model access to client-side state without exposing it to the server.

### How client tools work

1. The server's `SecurityOptions` maintains an allowlist of tool names the client may call
2. When the AI model requests a tool call, the `ProxyChatClient` checks if it's a client tool
3. If so, the tool runs locally in the browser
4. The result is submitted back to the server as a tool result message

### Create a client tool

Add a new tool class in `src/SecureProxyChatClients.Client.Web/Tools/`:

```csharp
public sealed class MyNewTool(SomeDependency dep)
{
    [Description("Does something useful on the client side")]
    public MyResult MyAction([Description("Parameter description")] string input)
    {
        return new MyResult(dep.DoSomething(input));
    }
}
```

### Register the client tool

Register it in `src/SecureProxyChatClients.Client.Web/Tools/ClientToolRegistry.cs`:

```csharp
public sealed class ClientToolRegistry
{
    public ClientToolRegistry(
        GetStoryGraphTool getStoryGraph,
        SearchStoryTool searchStory,
        SaveStoryStateTool saveStoryState,
        GetWorldRulesTool getWorldRules,
        MyNewTool myNewTool)          // Inject the new tool
    {
        _tools =
        [
            // ... existing tools
            AIFunctionFactory.Create(myNewTool.MyAction),   // Register
        ];
    }
}
```

### Allowlist the tool name

Add the tool's method name to the allowlist in `src/SecureProxyChatClients.Server/Security/SecurityOptions.cs`:

```csharp
public List<string> AllowedToolNames { get; set; } =
[
    "GetStoryGraph", "SearchStory", "SaveStoryState", "RollDice", "GetWorldRules",
    "MyAction"   // Must match the method name exactly
];
```

If you skip this step, the `InputValidator` will reject any request containing the tool name.

## Creating AI agents

The sample demonstrates a multi-agent pattern in `src/SecureProxyChatClients.Client.Web/Agents/`.

### LoreAgent

`LoreAgent` is a lightweight agent that wraps an `IChatClient` with a persona defined by a system prompt:

```csharp
public sealed class LoreAgent(string name, string emoji, string systemPrompt, IChatClient chatClient)
{
    public async Task<string> RespondAsync(IReadOnlyList<ChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };
        messages.AddRange(conversationHistory);
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Messages.Where(m => m.Role == ChatRole.Assistant && m.Text is { Length: > 0 })
            .Select(m => m.Text!).LastOrDefault() ?? string.Empty;
    }
}
```

### LoreAgentFactory

`LoreAgentFactory` creates the three built-in agents with their personality prompts:

- **Storyteller** 📖 — crafts narrative, drama, and dialogue
- **Critic** 🎭 — challenges ideas, identifies plot holes
- **Archivist** 📚 — maintains lore consistency, tracks facts

To add a new agent persona, add a factory method:

```csharp
// In LoreAgentFactory.cs
private const string CartographerPrompt = """
    You are the Cartographer — the spatial awareness expert of the Writer's Room.
    You track geography, distances, travel times, and spatial consistency...
    """;

public static LoreAgent CreateCartographer(IChatClient chatClient) =>
    new("Cartographer", "🗺️", CartographerPrompt, chatClient);
```

Then include it in `CreateAll()`:

```csharp
public static IReadOnlyList<LoreAgent> CreateAll(IChatClient chatClient) =>
[
    CreateStoryteller(chatClient),
    CreateCritic(chatClient),
    CreateArchivist(chatClient),
    CreateCartographer(chatClient),    // Add here
];
```

### WritersRoom

`WritersRoom` orchestrates round-robin discussions among agents. Each agent sees all previous responses, building on the conversation:

```csharp
public async IAsyncEnumerable<AgentMessage> RunDiscussionAsync(
    string userPitch, int maxRounds = 3, CancellationToken ct = default)
{
    var conversation = new List<ChatMessage> { new(ChatRole.User, userPitch) };

    for (int round = 0; round < maxRounds; round++)
        foreach (var agent in agents)
        {
            string response = await agent.RespondAsync(conversation, ct);
            conversation.Add(new ChatMessage(ChatRole.Assistant, $"[{agent.Name}]: {response}"));
            yield return new AgentMessage(agent.Name, agent.Emoji, response, isFinal);
        }
}
```

The `AgentMessage` record carries the agent's name, emoji, response text, and a flag indicating whether it's the final message in the discussion.

## Custom security policies

Security configuration lives in `src/SecureProxyChatClients.Server/Security/`.

### Blocked patterns

Add prompt injection patterns to `SecurityOptions.cs`:

```csharp
public List<string> BlockedPatterns { get; set; } =
[
    "ignore previous instructions",
    "ignore all previous",
    "disregard previous",
    "you are now",
    "pretend you are",
    "act as if you are",
    "new instructions:",
    "system prompt:",
    "override instructions",
    "forget your instructions",
    "ignore your instructions",
    "reveal your prompt",              // Add new patterns here
];
```

These patterns are checked case-insensitively by `InputValidator` against every user message.

### Input validation

`InputValidator.cs` implements six security controls:

| Control | What it does |
|---------|-------------|
| S1 | Strips `system`, `assistant`, and `tool` roles from the first message position |
| S3 | Detects prompt injection via `BlockedPatterns` |
| S4 | Enforces `MaxMessages`, `MaxMessageLength`, and `MaxTotalLength` limits |
| S5 | Validates tool names against `AllowedToolNames` |
| S6 | Detects HTML/script injection (`<script>`, `<iframe>`, `javascript:`, event handlers) |

To add a new validation rule, extend the `ValidateAndSanitize` method in `InputValidator.cs`.

### Output content filtering

`ContentFilter.cs` sanitizes LLM responses using compiled regexes (`[GeneratedRegex]`). To add a new filter pattern:

```csharp
[GeneratedRegex(@"your-pattern-here", RegexOptions.IgnoreCase)]
private static partial Regex YourNewPatternRegex();

public ChatResponse FilterResponse(ChatResponse response)
{
    // Add to the sanitization pipeline:
    sanitized = YourNewPatternRegex().Replace(sanitized, "[content removed]");
}
```

### Configuration via appsettings

All `SecurityOptions` values can be overridden in `appsettings.json`:

```json
{
  "Security": {
    "MaxMessages": 20,
    "MaxMessageLength": 8000,
    "BlockedPatterns": ["pattern1", "pattern2"]
  }
}
```

## Adding authentication providers

The sample uses ASP.NET Core Identity with bearer token endpoints, configured in `src/SecureProxyChatClients.Server/Program.cs`:

```csharp
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options => { ... })
    .AddEntityFrameworkStores<AppDbContext>();

// Later:
app.UseAuthentication();
app.MapIdentityApi<IdentityUser>();
```

### Plug in Entra ID

To add Microsoft Entra ID (Azure AD), add the `Microsoft.Identity.Web` package and configure alongside the existing Identity setup:

```csharp
builder.Services.AddAuthentication()
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
```

### Plug in external OAuth

For external OAuth providers (Google, GitHub, etc.), use ASP.NET Core's built-in extensions:

```csharp
builder.Services.AddAuthentication()
    .AddGoogle(options => { ... })
    .AddGitHub(options => { ... });
```

The client's `AuthState` service (`src/SecureProxyChatClients.Client.Web/Services/AuthState.cs`) stores the bearer token in session storage. The `AuthenticatedHttpMessageHandler` (`src/SecureProxyChatClients.Client.Web/Services/AuthenticatedHttpMessageHandler.cs`) automatically attaches `Authorization: Bearer {token}` to every request and clears auth state on 401 responses.

## Extending the game engine

Game engine components live in `src/SecureProxyChatClients.Server/GameEngine/`.

### Adding creatures to the Bestiary

`Bestiary.cs` defines creatures as a static `IReadOnlyList<Creature>`. Add a new creature to the list:

```csharp
new("Fire Elemental", "🔥", 5, 60, 14, 12,
    "A swirling column of living flame that scorches everything nearby.",
    ["Fire Aura (damages nearby enemies each turn)", "Flame Jet (ranged attack)"],
    "Water", XpReward: 120, GoldDrop: 30),
```

The `Bestiary` provides helper methods:
- `GetCreaturesForLevel(int level)` — returns creatures within ±2 levels of the player
- `GetEncounterCreature(int level)` — picks a random level-appropriate creature
- `FormatForDmPrompt(int level)` — generates a creature list for the DM system prompt

### Adding locations to the WorldMap

`WorldMap.cs` defines 12 locations with coordinates, connections, and emoji icons. Add a new location:

```csharp
new("Frozen Peaks", "🏔️", X: 6, Y: 0,
    Connections: ["Crystal Caverns", "Dragon's Peak"]),
```

The `WorldMap` provides:
- `GenerateMap()` — renders an ASCII map showing current, visited, adjacent, and unknown locations
- `GetConnections(string location)` — returns available destinations from a location

### Adding achievements

`Achievements.cs` defines 21 achievements across five categories: combat, exploration, social, wealth, and progression. Add a new achievement by adding an entry to the list and a check in `CheckAchievements`:

```csharp
new Achievement("mountain-climber", "Mountain Climber", "🏔️", "Reach the Frozen Peaks", "exploration"),
```

The `CheckAchievements` method evaluates `PlayerState` and returns newly earned achievements based on state like visited locations, inventory count, gold, level, XP, and event-based triggers.

### Adding Twists of Fate

`TwistOfFate.cs` defines 16 dramatic random events across four categories: environment, combat, encounter, discovery, and personal. Each twist is a record:

```csharp
new("Storm of Blades", "Spectral weapons materialize and orbit you threateningly...", "⚔️", "combat"),
```

Use `GetRandomTwist()` for a random event or `GetTwistByCategory(string category)` for a category-specific event. If the category has no matches, it falls back to a random twist.

## Custom AI providers

The AI provider is configured in `src/SecureProxyChatClients.Server/AI/AiServiceExtensions.cs` via a switch statement:

```csharp
string provider = configuration.GetValue<string>("AI:Provider") ?? "Fake";

switch (provider.ToLowerInvariant())
{
    case "fake":
        services.AddSingleton<IChatClient>(sp =>
            new ObservabilityChatClient(new FakeChatClient()));
        break;

    case "copilotcli":
        string model = configuration.GetValue<string>("AI:CopilotCli:Model") ?? "gpt-5-mini";
        services.AddSingleton<IChatClient>(sp =>
            new ObservabilityChatClient(
                new CopilotCliChatClient(sp.GetRequiredService<ILogger<CopilotCliChatClient>>(), model)));
        break;

    case "azureopenai":
        string endpoint = configuration["AI:Endpoint"] ?? throw new InvalidOperationException("...");
        string apiKey = configuration["AI:ApiKey"] ?? throw new InvalidOperationException("...");
        string deploymentName = configuration["AI:DeploymentName"] ?? "gpt-4o";
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        IChatClient innerClient = azureClient.GetChatClient(deploymentName).AsIChatClient();
        services.AddSingleton<IChatClient>(new ObservabilityChatClient(innerClient));
        break;

    default:
        throw new InvalidOperationException($"Unknown AI provider: {provider}");
}
```

### Add a new provider

1. Create a class that implements `IChatClient` (from `Microsoft.Extensions.AI`)
2. Add a new `case` to the switch statement
3. Wrap it in `ObservabilityChatClient` for consistent logging

```csharp
case "ollama":
    string ollamaEndpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
    string ollamaModel = configuration["AI:Ollama:Model"] ?? "llama3";
    services.AddSingleton<IChatClient>(sp =>
        new ObservabilityChatClient(new OllamaChatClient(ollamaEndpoint, ollamaModel)));
    break;
```

Set the provider via configuration:

```bash
# Environment variable
AI__Provider=ollama

# Command line
dotnet run -- --AI:Provider=ollama

# appsettings.json
{ "AI": { "Provider": "ollama" } }
```

### ObservabilityChatClient

All providers are wrapped in `ObservabilityChatClient` (`src/SecureProxyChatClients.Server/AI/ObservabilityChatClient.cs`), which provides structured logging for every request and response. Keep this wrapper when adding new providers.

## Adding a vector store

The `IStoryMemoryService` interface (`src/SecureProxyChatClients.Server/VectorStore/StoryMemoryService.cs`) abstracts semantic memory storage:

```csharp
public interface IStoryMemoryService
{
    Task StoreMemoryAsync(string userId, string sessionId, string content, string memoryType,
        float[]? embedding = null, CancellationToken ct = default);

    Task<IReadOnlyList<StoryMemory>> SearchAsync(string userId, float[] queryEmbedding,
        int limit = 5, CancellationToken ct = default);

    Task<IReadOnlyList<StoryMemory>> GetRecentMemoriesAsync(string userId,
        int limit = 10, CancellationToken ct = default);
}
```

### Existing implementations

| Implementation | File | Use case |
|----------------|------|----------|
| `InMemoryStoryMemoryService` | `StoryMemoryService.cs` | Dev/test fallback — stores memories in a list, no vector search |
| `PgVectorStoryMemoryService` | `StoryMemoryService.cs` | Production — uses PostgreSQL with pgvector for cosine distance (`<=>`) semantic search |

### Create a custom implementation

Implement `IStoryMemoryService` with your preferred vector database:

```csharp
public sealed class QdrantStoryMemoryService(QdrantClient client, ILogger<QdrantStoryMemoryService> logger)
    : IStoryMemoryService
{
    public async Task StoreMemoryAsync(string userId, string sessionId,
        string content, string memoryType, float[]? embedding = null, CancellationToken ct = default)
    {
        // Store in Qdrant
    }

    public async Task<IReadOnlyList<StoryMemory>> SearchAsync(
        string userId, float[] queryEmbedding, int limit = 5, CancellationToken ct = default)
    {
        // Cosine similarity search in Qdrant
    }

    public async Task<IReadOnlyList<StoryMemory>> GetRecentMemoriesAsync(
        string userId, int limit = 10, CancellationToken ct = default)
    {
        // Timestamp-based retrieval from Qdrant
    }
}
```

Register in DI:

```csharp
services.AddSingleton<IStoryMemoryService, QdrantStoryMemoryService>();
```

The `VectorDbContext` (`src/SecureProxyChatClients.Server/VectorStore/VectorDbContext.cs`) is the EF Core context used by the pgvector implementation. If your vector store doesn't use EF Core, you can skip it entirely and just implement the interface.
