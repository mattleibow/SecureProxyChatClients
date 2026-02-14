using System.Net.Http.Json;
using System.Text.Json;

namespace SecureProxyChatClients.Tests.Smoke;

/// <summary>
/// Smoke tests verify the application starts successfully and core endpoints respond.
/// These are fast, lightweight checks — no complex business logic validation.
/// </summary>
public class SmokeTests : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);

    private DistributedApplication _app = null!;
    private HttpClient _serverClient = null!;

    public async Task InitializeAsync()
    {
        var cts = new CancellationTokenSource(StartupTimeout);
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.SecureProxyChatClients_AppHost>(
                ["--AI:Provider=Fake"], cts.Token);

        _app = await appHost.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("server", cts.Token)
            .WaitAsync(StartupTimeout, cts.Token);

        _serverClient = _app.CreateHttpClient("server");
    }

    [Fact]
    public async Task Server_Health_Endpoint_Returns_Healthy()
    {
        var response = await _serverClient.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Server_Alive_Endpoint_Returns_Healthy()
    {
        var response = await _serverClient.GetAsync("/alive");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Server_Ping_Requires_Auth()
    {
        var response = await _serverClient.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Server_Exposes_Security_Headers()
    {
        var response = await _serverClient.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }

    [Fact]
    public async Task Server_Register_Endpoint_Accepts_Post()
    {
        var response = await _serverClient.PostAsJsonAsync("/register", new
        {
            email = $"smoke-{Guid.NewGuid():N}@test.com",
            password = "SmokeTestPassword1!"
        });

        Assert.True(response.IsSuccessStatusCode,
            $"Register returned {response.StatusCode}");
    }

    [Fact]
    public async Task Server_Login_Returns_Token()
    {
        string email = $"smoke-login-{Guid.NewGuid():N}@test.com";
        const string password = "SmokeTestPassword1!";

        await _serverClient.PostAsJsonAsync("/register", new { email, password });
        var loginResponse = await _serverClient.PostAsJsonAsync("/login", new { email, password });

        loginResponse.EnsureSuccessStatusCode();
        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("accessToken", out _), "Login response should contain accessToken");
    }

    public async Task DisposeAsync()
    {
        _serverClient.Dispose();
        await _app.DisposeAsync();
    }
}
