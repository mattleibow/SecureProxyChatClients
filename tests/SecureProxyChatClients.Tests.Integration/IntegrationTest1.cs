using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Integration.Tests;

public class ChatEndpointTests : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private DistributedApplication _app = null!;
    private HttpClient _authedClient = null!;
    private HttpClient _unauthClient = null!;

    public async Task InitializeAsync()
    {
        var cts = new CancellationTokenSource(DefaultTimeout);
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>(
                ["--AI:Provider=Fake", "--SeedUser:Password=Test123!"], cts.Token);

        _app = await appHost.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("server", cts.Token)
            .WaitAsync(DefaultTimeout, cts.Token);

        _unauthClient = _app.CreateHttpClient("server");
        _authedClient = _app.CreateHttpClient("server");

        // Authenticate via Bearer token (API endpoints are Bearer-only)
        var loginResponse = await _authedClient.PostAsJsonAsync("/login", new
        {
            email = "test@test.com",
            password = "Test123!"
        }, cts.Token);

        if (!loginResponse.IsSuccessStatusCode)
        {
            string body = await loginResponse.Content.ReadAsStringAsync(cts.Token);
            throw new InvalidOperationException($"Login failed: {loginResponse.StatusCode} {body}");
        }

        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
        string accessToken = json.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("No accessToken in login response");

        _authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    [Fact]
    public async Task Chat_Returns_Response_Through_Proxy()
    {
        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hello" }]
        };

        HttpResponseMessage response = await _authedClient.PostAsJsonAsync("/api/chat", request);

        response.EnsureSuccessStatusCode();
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.NotEmpty(chatResponse.Messages);
    }

    [Fact]
    public async Task Stream_Returns_SSE_Events()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        request.Content = JsonContent.Create(new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hello" }]
        });

        HttpResponseMessage response = await _authedClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: done", body);
    }

    [Fact]
    public async Task Unauthenticated_Request_Returns_401()
    {
        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hello" }]
        };

        HttpResponseMessage response = await _unauthClient.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_Rejects_Invalid_Input()
    {
        var request = new ChatRequest { Messages = [] };

        HttpResponseMessage response = await _authedClient.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _authedClient.Dispose();
        _unauthClient.Dispose();
        await _app.DisposeAsync();
    }
}
