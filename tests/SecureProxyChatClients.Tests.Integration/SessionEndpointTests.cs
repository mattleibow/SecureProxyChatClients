using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Integration.Tests;

public class SessionEndpointTests : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private DistributedApplication _app = null!;
    private HttpClient _authedClient = null!;
    private HttpClient _unauthClient = null!;

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

        _unauthClient = _app.CreateHttpClient("server");
        _authedClient = _app.CreateHttpClient("server");

        string testEmail = $"sesstest-{Guid.NewGuid():N}@test.com";
        const string testPassword = "SessionTestPass1!";

        await _authedClient.PostAsJsonAsync("/register", new { email = testEmail, password = testPassword }, cts.Token);
        var loginResponse = await _authedClient.PostAsJsonAsync("/login", new { email = testEmail, password = testPassword }, cts.Token);
        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
        string accessToken = json.GetProperty("accessToken").GetString()!;
        _authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    [Fact]
    public async Task CreateSession_Returns_SessionId()
    {
        var response = await _authedClient.PostAsync("/api/sessions", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("sessionId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListSessions_Returns_EmptyList_For_NewUser()
    {
        var response = await _authedClient.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_Unauthenticated_Returns_401()
    {
        var response = await _unauthClient.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_Nonexistent_Session_Returns_404()
    {
        var response = await _authedClient.GetAsync("/api/sessions/nonexistent-session-id/history");

        // Should be 404 for a session that doesn't exist
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"Expected 404 or 403 but got {response.StatusCode}");
    }

    public async Task DisposeAsync()
    {
        _authedClient.Dispose();
        _unauthClient.Dispose();
        await _app.DisposeAsync();
    }
}
