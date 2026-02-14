using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Integration.Tests;

/// <summary>
/// Tests that verify session isolation — users cannot access other users' data.
/// </summary>
public class SessionIsolationTests : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    private DistributedApplication _app = null!;
    private HttpClient _userAClient = null!;
    private HttpClient _userBClient = null!;

    public async Task InitializeAsync()
    {
        var cts = new CancellationTokenSource(DefaultTimeout);
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>(
                ["--AI:Provider=Fake"], cts.Token);

        _app = await appHost.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("server", cts.Token)
            .WaitAsync(DefaultTimeout, cts.Token);

        // Register and authenticate two separate users
        _userAClient = await CreateAuthenticatedClient($"usera-{Guid.NewGuid():N}@test.com", "UserAPassword1!", cts.Token);
        _userBClient = await CreateAuthenticatedClient($"userb-{Guid.NewGuid():N}@test.com", "UserBPassword1!", cts.Token);
    }

    private async Task<HttpClient> CreateAuthenticatedClient(string email, string password, CancellationToken ct)
    {
        var client = _app.CreateHttpClient("server");
        await client.PostAsJsonAsync("/register", new { email, password }, ct);
        var loginResponse = await client.PostAsJsonAsync("/login", new { email, password }, ct);
        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        string token = json.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task UserA_Cannot_See_UserB_Sessions()
    {
        // User A creates a session and sends a message
        var chatRequest = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hello from user A" }]
        };
        var chatResponse = await _userAClient.PostAsJsonAsync("/api/chat", chatRequest);
        chatResponse.EnsureSuccessStatusCode();

        var chatResult = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResult?.SessionId);

        // User B should not see User A's session in their list
        var sessionsResponse = await _userBClient.GetAsync("/api/sessions");
        sessionsResponse.EnsureSuccessStatusCode();
        var sessions = await sessionsResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain(chatResult.SessionId, sessions);
    }

    [Fact]
    public async Task UserB_Cannot_Access_UserA_Session_History()
    {
        // User A creates a session
        var chatRequest = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Secret from user A" }]
        };
        var chatResponse = await _userAClient.PostAsJsonAsync("/api/chat", chatRequest);
        chatResponse.EnsureSuccessStatusCode();
        var chatResult = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();

        // User B tries to access User A's session history
        var historyResponse = await _userBClient.GetAsync($"/api/sessions/{chatResult!.SessionId}/history");

        // Should be denied (403 or 404 — implementation may vary)
        Assert.True(
            historyResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403/404 but got {historyResponse.StatusCode}");
    }

    public async Task DisposeAsync()
    {
        _userAClient.Dispose();
        _userBClient.Dispose();
        await _app.DisposeAsync();
    }
}
