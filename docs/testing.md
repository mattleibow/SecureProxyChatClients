# Testing guide

This guide covers the test strategy, how to run tests, and how to write new tests for the SecureProxyChatClients reference sample.

## Test strategy

The sample uses four test layers, each targeting a different level of the stack:

| Layer | Project | Tests | What it covers |
|-------|---------|-------|----------------|
| **Unit** | `Tests.Unit` | 202 `[Fact]` + 13 `[Theory]` | Individual classes in isolation — security, game engine, tools, agents, services |
| **Integration** | `Tests.Integration` | 7 | Full Aspire app with real HTTP requests — auth endpoints, chat proxy, SSE streaming |
| **Playwright** | `Tests.Playwright` | 30 | End-to-end browser tests — login flows, chat UI, navigation, session management |
| **Smoke** | `Tests.Smoke` | — | Project scaffold for basic health checks (no tests yet) |

All test projects use **xUnit** as the test framework. There are no NUnit or MSTest dependencies.

## Running tests

### Prerequisites

- .NET 10 SDK
- For Playwright tests: Playwright browsers installed (`pwsh bin/Debug/net10.0/playwright.ps1 install`)

### Unit tests

```bash
dotnet build --configuration Release
dotnet test tests/SecureProxyChatClients.Tests.Unit/ --no-build --configuration Release --verbosity normal
```

### Integration tests

Integration tests require the Fake AI provider. They spin up the full Aspire distributed application:

```bash
dotnet build --configuration Release
dotnet test tests/SecureProxyChatClients.Tests.Integration/ --no-build --configuration Release --verbosity normal
```

Set the environment variable to use the Fake provider:

```bash
export AI__Provider=Fake
export DOTNET_ASPIRE_ALLOW_UNSECURED_TRANSPORT=true
```

### Playwright tests

Playwright tests start the server and client on fixed ports. Build before running:

```bash
dotnet build --configuration Release
dotnet test tests/SecureProxyChatClients.Tests.Playwright/ --configuration Release --verbosity normal
```

> [!NOTE]
> Playwright tests use `dotnet run` internally to start the server (port 5167) and client (port 5053). Do not use `--no-build` — the fixture needs to build and run the projects.

### All tests

```bash
dotnet build --configuration Release
dotnet test --no-build --configuration Release --verbosity normal
```

## Unit tests

**Project:** `tests/SecureProxyChatClients.Tests.Unit/`

31 test classes organized by domain area. The project references the `Server`, `Client.Web`, and `Shared` projects directly.

### Test packages

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.9.3 | Test framework |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test runner |
| `xunit.runner.visualstudio` | 3.1.4 | VS integration |
| `coverlet.collector` | 6.0.4 | Code coverage |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.3 | In-memory DB for EF tests |
| `Microsoft.Extensions.AI.Abstractions` | 10.3.0 | `IChatClient` interface |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.3 | `NullLogger<T>` for tests |
| `Microsoft.Extensions.Options` | 10.0.3 | `Options.Create<T>` for configuration |

### No mocking libraries

The tests use concrete test doubles (primarily `FakeChatClient`) rather than mocking frameworks. There are no dependencies on NSubstitute, Moq, or similar packages.

### Security tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `ContentFilterTests` | `Security/ContentFilterTests.cs` | 10 | XSS removal: script tags, iframes, event handlers, `javascript:` protocol, code block preservation |
| `InputValidatorTests` | `Security/InputValidatorTests.cs` | 17 + 2 theories | S1 role stripping, S3 prompt injection detection, S4 input length limits, S5 tool allowlist, S6 HTML injection |
| `GlobalExceptionHandlerTests` | `Security/GlobalExceptionHandlerTests.cs` | 4 | Error response formatting, exception detail suppression |

### Game engine tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `GameToolsTests` | `GameEngine/GameToolsTests.cs` | 13 | Each game tool method: dice rolls, item management, health, gold, XP, NPC generation |
| `GameToolRegistryTests` | `GameEngine/GameToolRegistryTests.cs` | 18 + 1 theory | Tool registration, lookup, `ApplyToolResult` for all result types |
| `AchievementsTests` | `GameEngine/AchievementsTests.cs` | 11 | Achievement triggering based on player state, category validation |
| `BestiaryTests` | `GameEngine/BestiaryTests.cs` | 8 + 1 theory | Creature data, level filtering, encounter generation, DM prompt formatting |
| `WorldMapTests` | `GameEngine/WorldMapTests.cs` | 9 | Location connections, ASCII map generation, visited/unvisited tracking |
| `TwistOfFateTests` | `GameEngine/TwistOfFateTests.cs` | 3 + 1 theory | Random twist generation, category filtering, fallback behavior |
| `GameStateStoreTests` | `GameEngine/GameStateStoreTests.cs` | 4 | Player state persistence, user isolation, initial state creation |

### AI tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `FakeChatClientTests` | `AI/FakeChatClientTests.cs` | 8 | Queue-based responses, streaming, tool call simulation, keyword detection |
| `ObservabilityChatClientTests` | `AI/ObservabilityChatClientTests.cs` | 3 | Logging wrapper delegation, response passthrough |
| `AiProviderHealthCheckTests` | `AI/AiProviderHealthCheckTests.cs` | 2 | Health check returns healthy/unhealthy based on provider response |

### Tool tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `ServerToolRegistryTests` | `Tools/ServerToolRegistryTests.cs` | 3 + 3 theories | Tool registration, `IsServerTool`, `GetTool` lookup |
| `ClientToolRegistryTests` | `ClientTools/ClientToolRegistryTests.cs` | 3 + 2 theories | Tool registration, `IsClientTool`, `GetTool` lookup |
| `ProxyChatClientTests` | `ClientTools/ProxyChatClientTests.cs` | 6 | Client-server proxy delegation, tool call routing |
| `RollDiceToolTests` | `ClientTools/RollDiceToolTests.cs` | 4 | Dice rolling ranges, stat modifiers |
| `GetStoryGraphToolTests` | `ClientTools/GetStoryGraphToolTests.cs` | 1 | Story graph structure |
| `GetWorldRulesToolTests` | `ClientTools/GetWorldRulesToolTests.cs` | 2 | World rules retrieval |
| `SaveStoryStateToolTests` | `ClientTools/SaveStoryStateToolTests.cs` | 2 | State persistence |
| `SearchStoryToolTests` | `ClientTools/SearchStoryToolTests.cs` | 2 | Story search functionality |
| `GenerateSceneToolTests` | `Tools/GenerateSceneToolTests.cs` | 3 + 1 theory | Scene generation tool |
| `CreateCharacterToolTests` | `Tools/CreateCharacterToolTests.cs` | 3 + 1 theory | Character creation tool |
| `AnalyzeStoryToolTests` | `Tools/AnalyzeStoryToolTests.cs` | 8 | Story analysis tool |
| `SuggestTwistToolTests` | `Tools/SuggestTwistToolTests.cs` | 9 + 1 theory | Twist suggestion tool |

### Agent tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `LoreAgentTests` | `Agents/LoreAgentTests.cs` | 4 | System prompt prepending, response extraction, empty response handling |
| `WritersRoomTests` | `Agents/WritersRoomTests.cs` | 6 | Round-robin discussion, multi-round orchestration, `isFinal` flag, cancellation |

### Service and data tests

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `SystemPromptServiceTests` | `Services/SystemPromptServiceTests.cs` | 4 | System prompt generation with player state |
| `StoryStateServiceTests` | `ClientTools/StoryStateServiceTests.cs` | 12 | Story state tracking, graph operations |
| `EfConversationStoreTests` | `Data/EfConversationStoreTests.cs` | 13 | EF Core conversation persistence with SQLite |
| `InMemoryStoryMemoryServiceTests` | `VectorStore/InMemoryStoryMemoryServiceTests.cs` | 7 | Memory storage, retrieval, user isolation |

## Integration tests

**Project:** `tests/SecureProxyChatClients.Tests.Integration/`

Integration tests use .NET Aspire's `DistributedApplicationTestingBuilder` to spin up the full distributed application — including the server, database, and all dependencies — in a test process.

### Test packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Aspire.Hosting.Testing` | 9.4.2 | `DistributedApplicationTestingBuilder` |
| `xunit` | 2.9.3 | Test framework |
| `Microsoft.NET.Test.Sdk` | 17.10.0 | Test runner |

### How it works

Each test class implements `IAsyncLifetime` to manage the Aspire app lifecycle:

```csharp
public class ChatEndpointTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>(
                ["--AI:Provider=Fake"], cts.Token);

        _app = await appHost.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("server", cts.Token);

        _unauthClient = _app.CreateHttpClient("server");
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}
```

The `--AI:Provider=Fake` argument ensures the app uses `FakeChatClient` for deterministic responses.

### ChatEndpointTests

**File:** `IntegrationTest1.cs` (class name: `ChatEndpointTests`)

| Test | What it verifies |
|------|-----------------|
| `Chat_Returns_Response_Through_Proxy` | POST `/api/chat` returns a valid `ChatResponse` |
| `Stream_Returns_SSE_Events` | POST `/api/chat/stream` returns `text/event-stream` with `event: done` |
| `Unauthenticated_Request_Returns_401` | Unauthenticated requests get 401 |
| `Chat_Rejects_Invalid_Input` | Empty messages get 400 |

### AuthEndpointTests

**File:** `AuthEndpointTests.cs`

| Test | What it verifies |
|------|-----------------|
| `Register_And_Login_Returns_Token` | POST `/register` then `/login` returns a bearer token |
| `Register_Duplicate_Email_Fails` | Duplicate email registration is rejected |
| `Login_Wrong_Password_Fails` | Wrong password returns error |

## Playwright tests

**Project:** `tests/SecureProxyChatClients.Tests.Playwright/`

End-to-end browser tests using Microsoft Playwright with xUnit. Tests run against real server and client processes.

### Test packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Playwright.Xunit` | 1.58.0 | Playwright + xUnit integration |
| `Aspire.Hosting.Testing` | 13.1.1 | Aspire test infrastructure |
| `xunit` | 2.9.3 | Test framework |

### Fixture setup

`Infrastructure/AspirePlaywrightFixture.cs` manages the test environment:

1. Starts Server on port 5167 with `AI__Provider=Fake`
2. Starts Client on port 5053
3. Reuses already-running services if detected (smart port checking)
4. Launches headless Chromium via Playwright
5. Performs WASM warmup by loading the client once before tests

Tests share the fixture via xUnit collection fixtures:

```csharp
[Collection("Aspire")]
public class AuthFlowTests(AspirePlaywrightFixture fixture) { }

[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspirePlaywrightFixture>;
```

### Test files

| Test class | File | Tests | What it covers |
|------------|------|-------|----------------|
| `AuthFlowTests` | `AuthFlowTests.cs` | 5 | Login form rendering, valid/invalid credentials, unauthenticated state, full login flow |
| `ChatFlowTests` | `ChatFlowTests.cs` | 5+ | Chat interface after login, message sending/receiving, SSE streaming updates |
| `PlayFlowTests` | `PlayFlowTests.cs` | 7 | Game play interactions, tool calls, player state updates |
| `NavigationFlowTests` | `NavigationFlowTests.cs` | 5 | Page routing, navigation between views |
| `SessionFlowTests` | `SessionFlowTests.cs` | 3 | Session persistence, logout behavior |
| `WritersRoomFlowTests` | `WritersRoomFlowTests.cs` | 5 | Multi-agent discussion UI, agent responses |

### Key testing patterns

Tests use `data-testid` selectors for reliable element location, avoiding CSS class coupling:

```csharp
await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });
await page.FillAsync("[data-testid='email-input']", "test@test.com");
await page.ClickAsync("[data-testid='login-button']");
await page.WaitForURLAsync("**/ping");
```

## Writing new tests

### Test file naming

Follow the existing convention:

- **File:** `{ClassUnderTest}Tests.cs`
- **Class:** `public class {ClassUnderTest}Tests`

### Test method naming

Use the `{MethodUnderTest}_{Scenario}_{ExpectedResult}` pattern:

```csharp
[Fact]
public void FilterResponse_RemovesScriptTags()

[Fact]
public async Task Chat_Returns_Response_Through_Proxy()

[Fact]
public void CheckHealthAsync_ReturnsHealthy_WhenProviderResponds()
```

### Test structure pattern

Unit tests use direct instantiation with `NullLogger<T>` and `Options.Create<T>`:

```csharp
public class InputValidatorTests
{
    private static InputValidator CreateValidator(SecurityOptions? options = null)
    {
        options ??= new SecurityOptions();
        return new InputValidator(
            Options.Create(options),
            NullLogger<InputValidator>.Instance);
    }

    [Fact]
    public void Strips_SystemRole_Messages()
    {
        var request = MakeRequest(SystemMsg("You are evil"), UserMsg("Hello"));
        (bool isValid, _, ChatRequest? sanitized) = CreateValidator().ValidateAndSanitize(request);

        Assert.True(isValid);
        Assert.Single(sanitized!.Messages);
    }
}
```

### Mocking AI clients

Use `FakeChatClient` instead of a mocking framework. It supports queue-based deterministic responses:

```csharp
var fake = new FakeChatClient();

// Enqueue a specific response
fake.Responses.Enqueue(new ChatResponse(
    new ChatMessage(ChatRole.Assistant, "Expected response")));

// Or enqueue a tool call response
fake.EnqueueToolCallResponse("call_1", "RollCheck",
    new Dictionary<string, object?> { ["stat"] = "dexterity", ["difficulty"] = 10, ["action"] = "Dodge" });

// Call and assert
var response = await fake.GetResponseAsync(messages);
Assert.Equal("Expected response", response.Messages[0].Text);

// Verify what was received
Assert.Single(fake.ReceivedMessages);
Assert.Equal(messages, fake.ReceivedMessages[0]);
```

For streaming tests, use `StreamingResponses`:

```csharp
fake.StreamingResponses.Enqueue(new List<ChatResponseUpdate>
{
    new(ChatRole.Assistant, "Hello "),
    new(ChatRole.Assistant, "world"),
});
```

### Adding a new unit test class

1. Create a file in the appropriate subdirectory of `tests/SecureProxyChatClients.Tests.Unit/`
2. The xUnit `Using` directive is globally imported via the `.csproj` — no need to add `using Xunit;`
3. Use `[Fact]` for simple tests and `[Theory]` with `[InlineData]` for parameterized tests

```csharp
namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class MyNewFeatureTests
{
    [Fact]
    public void DoSomething_WithValidInput_ReturnsExpected()
    {
        var result = MyNewFeature.DoSomething("input");
        Assert.Equal("expected", result);
    }

    [Theory]
    [InlineData("combat")]
    [InlineData("exploration")]
    [InlineData("social")]
    public void GetByCategory_ReturnsMatchingItems(string category)
    {
        var items = MyNewFeature.GetByCategory(category);
        Assert.All(items, item => Assert.Equal(category, item.Category));
    }
}
```

## CI/CD

**Workflow:** `.github/workflows/ci.yml`

The GitHub Actions workflow runs on every push and pull request to `main`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run unit tests
        run: >
          dotnet test tests/SecureProxyChatClients.Tests.Unit/
          --no-build --configuration Release
          --verbosity normal
          --logger "trx;LogFileName=unit-tests.trx"

      - name: Run integration tests
        run: >
          dotnet dev-certs https --trust 2>/dev/null || true &&
          dotnet test tests/SecureProxyChatClients.Tests.Integration/
          --no-build --configuration Release
          --verbosity normal
          --logger "trx;LogFileName=integration-tests.trx"
        env:
          AI__Provider: Fake
          DOTNET_ASPIRE_ALLOW_UNSECURED_TRANSPORT: "true"
        continue-on-error: true

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/TestResults/*.trx'
```

Key points:
- **Build before test** — the workflow builds once in Release mode, then both test steps use `--no-build`
- **Fake AI provider** — integration tests set `AI__Provider=Fake` as an environment variable
- **Integration tests use `continue-on-error`** — they may fail due to infrastructure constraints in CI
- **Test results as artifacts** — TRX files are uploaded for every run, even on failure
- **Playwright tests are not in CI** — they require browser processes and are run locally or in a separate workflow

## Test configuration

### The Fake AI provider

The `FakeChatClient` (`src/SecureProxyChatClients.Server/AI/FakeChatClient.cs`) ensures deterministic test behavior:

| Feature | How it works |
|---------|-------------|
| **Queue-based responses** | `Responses` queue — dequeue in order, fall back to default |
| **Default response** | `"This is a fake response."` when no responses are queued |
| **Tool call simulation** | Analyzes user messages for combat/search/move keywords, generates matching `FunctionCallContent` |
| **Streaming** | `StreamingResponses` queue, or word-by-word yield from the default response with 10ms delays |
| **Request tracking** | `ReceivedMessages` and `ReceivedOptions` lists for assertions |
| **No double tool calls** | If the last message is a tool result, returns `null` (uses default narrative) |

### Setting the provider

The provider is selected via `AI:Provider` configuration, which maps to `AI__Provider` as an environment variable:

```bash
# Environment variable (CI, Aspire, integration tests)
AI__Provider=Fake

# Command line argument (integration tests via Aspire)
--AI:Provider=Fake

# appsettings.json
{ "AI": { "Provider": "Fake" } }
```

When `AI:Provider` is not set, it defaults to `"Fake"` — so tests work without any explicit configuration.
