using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Integration.Tests;

public class PlayEndpointTests : IAsyncLifetime
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

        string testEmail = $"playtest-{Guid.NewGuid():N}@test.com";
        const string testPassword = "PlayTestPassword1!";

        await _authedClient.PostAsJsonAsync("/register", new { email = testEmail, password = testPassword }, cts.Token);
        var loginResponse = await _authedClient.PostAsJsonAsync("/login", new { email = testEmail, password = testPassword }, cts.Token);
        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
        string accessToken = json.GetProperty("accessToken").GetString()!;
        _authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    [Fact]
    public async Task NewGame_Creates_Player_State()
    {
        var response = await _authedClient.PostAsJsonAsync("/api/play/new-game", new
        {
            characterName = "TestHero",
            characterClass = "warrior"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("TestHero", body);
    }

    [Fact]
    public async Task NewGame_Validates_CharacterClass()
    {
        var response = await _authedClient.PostAsJsonAsync("/api/play/new-game", new
        {
            characterName = "TestHero",
            characterClass = "INJECT<script>alert(1)</script>"
        });

        // Should default to Explorer, not inject script
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>", body);
    }

    [Fact]
    public async Task Play_Unauthenticated_Returns_401()
    {
        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "I look around" }]
        };

        var response = await _unauthClient.PostAsJsonAsync("/api/play", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Twist_Returns_Random_Twist()
    {
        var response = await _authedClient.GetAsync("/api/play/twist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
    }

    public async Task DisposeAsync()
    {
        _authedClient.Dispose();
        _unauthClient.Dispose();
        await _app.DisposeAsync();
    }
}
