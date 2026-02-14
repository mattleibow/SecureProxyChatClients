using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace SecureProxyChatClients.Tests.Playwright.Infrastructure;

/// <summary>
/// xUnit collection fixture that starts Server + Client on fixed ports and Playwright browser.
/// </summary>
public sealed class AspirePlaywrightFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private Process? _clientProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private const int ServerPort = 5167;
    private const int ClientPort = 5053;

    public string ServerUrl => $"http://localhost:{ServerPort}";
    public string ClientUrl => $"http://localhost:{ClientPort}";
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");

    public async Task InitializeAsync()
    {
        var solutionRoot = FindSolutionRoot();

        // Start server
        _serverProcess = StartDotnetProcess(
            Path.Combine(solutionRoot, "src", "SecureProxyChatClients.Server"),
            ServerPort,
            new Dictionary<string, string>
            {
                ["Client__Origin"] = $"http://localhost:{ClientPort}",
                ["AI__Provider"] = "Fake",
            });

        // Start client
        _clientProcess = StartDotnetProcess(
            Path.Combine(solutionRoot, "src", "SecureProxyChatClients.Client.Web"),
            ClientPort,
            new Dictionary<string, string>());

        // Wait for server to be ready
        using var httpClient = new HttpClient();
        await WaitForHealthy(httpClient, $"{ServerUrl}/api/ping", TimeSpan.FromSeconds(30));

        // Wait for client to be ready
        await WaitForHealthy(httpClient, ClientUrl, TimeSpan.FromSeconds(30));

        // Start Playwright
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();

        KillProcess(_serverProcess);
        KillProcess(_clientProcess);
    }

    private static Process StartDotnetProcess(string projectDir, int port, Dictionary<string, string> envVars)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --no-build",
            WorkingDirectory = projectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.Environment["ASPNETCORE_URLS"] = $"http://localhost:{port}";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DOTNET_NOLOGO"] = "1";

        foreach (var (key, value) in envVars)
            psi.Environment[key] = value;

        var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process in {projectDir}");
        return process;
    }

    private static async Task WaitForHealthy(HttpClient httpClient, string url, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                // Any response means the server is up (even 401)
                return;
            }
            catch
            {
                await Task.Delay(500);
            }
        }
        throw new TimeoutException($"Service at {url} did not become healthy within {timeout}");
    }

    private static void KillProcess(Process? process)
    {
        if (process is null || process.HasExited) return;
        try
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
        }
        catch { /* best effort */ }
    }

    private static string FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0 || Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find solution root");
    }
}

[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspirePlaywrightFixture>;
