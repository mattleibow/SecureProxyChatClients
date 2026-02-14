# Code Recommendations & Patterns

> **Created**: 2026-02-13
> **Purpose**: Reference code snippets and patterns to use during implementation. These are starting points — adapt as needed.

## CORS Configuration (Server)

Server must allow cross-origin requests from the Blazor WASM app:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["Client:Origin"] ?? "https://localhost:5002")
              .AllowAnyMethod()
              .AllowAnyHeader();
        // Do NOT call AllowCredentials() — we use bearer tokens, not cookies
    });
});

// In the pipeline:
app.UseCors();
```

## IChatClient Implementations

### FakeChatClient (Unit/Integration Tests)

Deterministic `IChatClient` for testing without real AI:

```csharp
public class FakeChatClient : IChatClient
{
    public Queue<ChatResponse> Responses { get; } = new();
    public Queue<List<ChatResponseUpdate>> StreamingResponses { get; } = new();
    public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        ReceivedMessages.Add(messages);
        return Task.FromResult(Responses.Dequeue());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ReceivedMessages.Add(messages);
        foreach (var update in StreamingResponses.Dequeue())
        {
            yield return update;
            await Task.Delay(10, ct);
        }
    }
}
```

### CopilotCliChatClient (Dev — No Azure Setup)

Shells out to `copilot -p` for real AI during development:

```csharp
public class CopilotCliChatClient(string model = "gpt-5-mini") : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var lastMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        var prompt = lastMessage?.Text ?? string.Empty;

        var psi = new ProcessStartInfo("copilot")
        {
            Arguments = $"""-p "{EscapeForShell(prompt)}" --model {model} --available-tools "" """,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var cleanOutput = StripCopilotFooter(output);
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, cleanOutput)]);
    }

    private static string EscapeForShell(string input) =>
        input.Replace("\"", "\\\"").Replace("\n", " ");

    private static string StripCopilotFooter(string output)
    {
        var lines = output.Split('\n');
        var endIndex = lines.Length;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("Total usage") || lines[i].Contains("Continuing autonomously"))
            {
                endIndex = i;
                break;
            }
        }
        return string.Join('\n', lines[..endIndex]).TrimEnd();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await GetResponseAsync(messages, options, ct);
        foreach (var word in response.Messages[0].Text!.Split(' '))
        {
            yield return new ChatResponseUpdate { Text = word + " " };
            await Task.Delay(30, ct);
        }
    }
}
```

**JSON tool simulation**: For dev tool-call testing, include tool definitions in the system prompt and instruct the model to respond with JSON (`{"tool": "name", "args": {...}}`) when it wants to call a tool. Parse the output and convert to `FunctionCallContent`.

### AI Provider Configuration

```json
{
  "AI": {
    "Provider": "copilot-cli",
    "CopilotCli": { "Model": "gpt-5-mini" }
  }
}
```

Three providers: `fake` (CI), `copilot-cli` (dev), `azure-openai` (production).

## ProxyChatClient (Tool-Aware Loop) — Pseudocode

The client's `IChatClient` handles tool routing transparently. This is **pseudocode** — adapt to the real `ChatResponse` API (e.g., inspect `response.Messages.Last().Contents` for `FunctionCallContent`):

```csharp
// PSEUDOCODE — adapt property names to actual MEAI ChatResponse API
public async Task<ChatResponse> GetResponseAsync(IReadOnlyList<ChatMessage> messages, ...)
{
    while (true)
    {
        var response = await _httpClient.PostAndParseAsync("/api/chat", messages);

        if (response.HasToolCalls && IsClientTool(response.ToolCalls))
        {
            var results = await ExecuteClientToolsAsync(response.ToolCalls);
            messages = [..messages, response.Message, ..results];
            continue;
        }

        return response;
    }
}
```

## SSE Streaming (HttpClient in WASM)

Browser `EventSource` doesn't support POST or auth headers. Use `HttpClient` streaming:

```csharp
var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
request.Content = JsonContent.Create(chatRequest);
var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
var stream = await response.Content.ReadAsStreamAsync();
// Parse SSE events from stream
```

## Playwright E2E Tests (xUnit)

```csharp
public class ChatTests : PageTest, IAsyncLifetime
{
    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();

        var endpoint = _app.GetEndpoint("client-web");
        await Page.GotoAsync(endpoint.ToString());
    }

    [Fact]
    public async Task User_Can_Send_Message_And_See_Response()
    {
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "message" })
            .FillAsync("Hello, tell me a story");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();

        var responseArea = Page.GetByTestId("chat-response");
        await Expect(responseArea).ToContainTextAsync("Once upon", new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Streaming_Tokens_Appear_Progressively()
    {
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "message" })
            .FillAsync("Count from 1 to 5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();

        var response = Page.GetByTestId("chat-response");
        await Expect(response).Not.ToBeEmptyAsync();
        var firstLength = (await response.TextContentAsync())?.Length ?? 0;
        await Task.Delay(500);
        var secondLength = (await response.TextContentAsync())?.Length ?? 0;
        Assert.True(secondLength > firstLength, "Content should grow as tokens stream");
    }

    [Fact]
    public async Task Unauthenticated_User_Redirected_To_Login()
    {
        // Fresh navigation without in-memory token triggers redirect
        await Page.GotoAsync(_app.GetEndpoint("client-web") + "/chat");
        await Expect(Page).ToHaveURLAsync(new Regex("/login"));
    }

    [Fact]
    public async Task User_Can_Register_Login_And_Chat()
    {
        // Registration uses scaffolded Identity UI on server
        await Page.GotoAsync(_app.GetEndpoint("server") + "/Identity/Account/Register");

        await Page.GetByLabel("Email").FillAsync("newuser@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword1!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword1!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Navigate to WASM app, log in via REST API (bearer token)
        await Page.GotoAsync(_app.GetEndpoint("client-web") + "/login");
        await Page.GetByLabel("Email").FillAsync("newuser@test.com");
        await Page.GetByLabel("Password").FillAsync("TestPassword1!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "message" })
            .FillAsync("Hello from a new user");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();
        await Expect(Page.GetByTestId("chat-response")).Not.ToBeEmptyAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

**Playwright patterns:**
- `data-testid` attributes on all interactive elements
- `Expect(...).ToContainTextAsync(...)` with timeouts for streaming
- Progressive content checks for streaming validation
- Registration on server app, login on WASM app

## Integration Tests (Aspire HTTP-level — xUnit)

```csharp
public class ChatEndpointTests : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        _client = _app.CreateHttpClient("server");

        // Authenticate with seeded test user
        var loginResponse = await _client.PostAsJsonAsync("/login", new { email = "test@test.com", password = "TestPassword1!" });
        var token = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    [Fact]
    public async Task Chat_Returns_Response_Through_Proxy()
    {
        var request = new ChatRequest { Message = "Hello" };
        var response = await _client.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Stream_Returns_SSE_Events()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        request.Content = JsonContent.Create(new ChatRequest { Message = "Hello" });
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unauthenticated_Request_Returns_401()
    {
        using var unauthClient = _app.CreateHttpClient("server");
        var response = await unauthClient.PostAsJsonAsync("/api/chat", new ChatRequest { Message = "Hello" });
        Assert.Equal(401, (int)response.StatusCode);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}
```
