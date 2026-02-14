using Microsoft.Playwright;
using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class AuthFlowTests(AspirePlaywrightFixture fixture)
{
    [Fact]
    public async Task LoginPage_ShowsForm()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/login");
            await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

            var emailInput = page.Locator("[data-testid='login-email']");
            var passwordInput = page.Locator("[data-testid='login-password']");
            var submitButton = page.Locator("[data-testid='login-submit']");

            Assert.True(await emailInput.IsVisibleAsync());
            Assert.True(await passwordInput.IsVisibleAsync());
            Assert.True(await submitButton.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToPing()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/login");
            await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

            await page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
            await page.Locator("[data-testid='login-password']").FillAsync("Test123!");
            await page.Locator("[data-testid='login-submit']").ClickAsync();

            // Should redirect to /ping
            await page.WaitForURLAsync($"**/ping", new() { Timeout = 15_000 });

            Assert.Contains("/ping", page.Url);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/login");
            await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

            await page.Locator("[data-testid='login-email']").FillAsync("wrong@wrong.com");
            await page.Locator("[data-testid='login-password']").FillAsync("Wrong123!");
            await page.Locator("[data-testid='login-submit']").ClickAsync();

            // Should show error
            var errorDiv = page.Locator("[data-testid='login-error']");
            await errorDiv.WaitForAsync(new() { Timeout = 15_000 });

            Assert.True(await errorDiv.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task PingPage_WithoutAuth_ShowsUnauthenticatedMessage()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/ping");
            await page.WaitForSelectorAsync("[data-testid='ping-unauthenticated']", new() { Timeout = 30_000 });

            var unauthDiv = page.Locator("[data-testid='ping-unauthenticated']");
            Assert.True(await unauthDiv.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task FullFlow_Login_ThenPing_ShowsAuthenticatedResult()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            // Login first
            await page.GotoAsync($"{fixture.ClientUrl}/login");
            await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

            await page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
            await page.Locator("[data-testid='login-password']").FillAsync("Test123!");
            await page.Locator("[data-testid='login-submit']").ClickAsync();

            // Wait for redirect to /ping, then do a full page load to ensure sessionStorage is used
            await page.WaitForURLAsync($"**/ping", new() { Timeout = 15_000 });
            await page.GotoAsync($"{fixture.ClientUrl}/ping");
            await page.WaitForSelectorAsync("[data-testid='ping-button']", new() { Timeout = 30_000 });

            // Wait for button to become enabled (auth loaded from sessionStorage)
            await page.WaitForFunctionAsync(
                "() => !document.querySelector('[data-testid=\"ping-button\"]')?.disabled",
                null,
                new() { Timeout = 10_000 });

            var pingButton = page.Locator("[data-testid='ping-button']");
            await pingButton.ClickAsync();

            // Wait for either result or error to appear
            var resultOrError = page.Locator("[data-testid='ping-result'], [data-testid='ping-error']");
            await resultOrError.First.WaitForAsync(new() { Timeout = 15_000 });

            // Check which one appeared
            var resultDiv = page.Locator("[data-testid='ping-result']");
            if (await resultDiv.CountAsync() > 0 && await resultDiv.IsVisibleAsync())
            {
                var resultText = await resultDiv.TextContentAsync();
                Assert.Contains("test@test.com", resultText!);
                Assert.Contains("True", resultText!);
            }
            else
            {
                var errorDiv = page.Locator("[data-testid='ping-error']");
                var errorText = await errorDiv.TextContentAsync();
                Assert.Fail($"Ping failed with error: {errorText}");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
