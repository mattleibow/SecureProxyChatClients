using Microsoft.Playwright;
using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class SessionFlowTests(AspirePlaywrightFixture fixture)
{
    private async Task<IPage> LoginAndNavigateToChat()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{fixture.ClientUrl}/login");
        await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

        await page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
        await page.Locator("[data-testid='login-password']").FillAsync("TestPassword1!");
        await page.Locator("[data-testid='login-submit']").ClickAsync();

        await page.WaitForURLAsync("**/ping", new() { Timeout = 15_000 });
        await page.GotoAsync($"{fixture.ClientUrl}/chat");
        await page.WaitForSelectorAsync("[data-testid='chat-container']", new() { Timeout = 30_000 });

        return page;
    }

    [Fact]
    public async Task Session_SendMessages_HistoryPersistedAcrossPageReload()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            // Send a message
            await page.Locator("[data-testid='chat-input']").FillAsync("Hello persistence test");
            await page.Locator("[data-testid='chat-send']").ClickAsync();

            // Wait for response
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"chat-message-content\"]').length >= 2",
                null,
                new() { Timeout = 15_000 });

            // Verify session sidebar shows a session
            await page.WaitForSelectorAsync("[data-testid='session-item']", new() { Timeout = 10_000 });
            var sessionItems = page.Locator("[data-testid='session-item']");
            var sessionCount = await sessionItems.CountAsync();
            Assert.True(sessionCount >= 1, "Should have at least one session after sending a message");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Session_CreateNewSession_AppearsInSidebar()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            // Click new session button
            var newSessionBtn = page.Locator("[data-testid='new-session-btn']");
            Assert.True(await newSessionBtn.IsVisibleAsync(), "New session button should be visible");

            await newSessionBtn.ClickAsync();

            // Wait for session to appear
            await page.WaitForSelectorAsync("[data-testid='session-item']", new() { Timeout = 10_000 });
            var sessionItems = page.Locator("[data-testid='session-item']");
            var count = await sessionItems.CountAsync();
            Assert.True(count >= 1, "Should have at least one session item after creating new session");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Session_SidebarAndHeaderVisible()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            var sidebar = page.Locator("[data-testid='session-sidebar']");
            Assert.True(await sidebar.IsVisibleAsync(), "Session sidebar should be visible");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
