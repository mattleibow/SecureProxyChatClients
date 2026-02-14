using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SecureProxyChatClients.Tests.Integration.Tests;

public class AuthEndpointTests : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private DistributedApplication _app = null!;
    private HttpClient _client = null!;

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

        _client = _app.CreateHttpClient("server");
    }

    [Fact]
    public async Task Register_And_Login_Returns_Token()
    {
        string email = $"newuser-{Guid.NewGuid():N}@test.com";

        var registerResponse = await _client.PostAsJsonAsync("/register", new
        {
            email,
            password = "StrongPassword1!"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/login", new
        {
            email,
            password = "StrongPassword1!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("accessToken", out var token));
        Assert.False(string.IsNullOrEmpty(token.GetString()));
    }

    [Fact]
    public async Task Register_Duplicate_Email_Fails()
    {
        string email = $"dup-{Guid.NewGuid():N}@test.com";

        var first = await _client.PostAsJsonAsync("/register", new { email, password = "StrongPassword1!" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/register", new { email, password = "StrongPassword2!" });
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Login_Wrong_Password_Fails()
    {
        var response = await _client.PostAsJsonAsync("/login", new
        {
            email = "nonexistent@test.com",
            password = "WrongPass1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
