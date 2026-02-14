using Microsoft.Playwright;
using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class NavigationFlowTests(AspirePlaywrightFixture fixture)
{
    [Fact]
    public async Task HomePage_ShowsLoreEngineBranding()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(fixture.ClientUrl);
            await page.WaitForSelectorAsync("[data-testid='home-container']", new() { Timeout = 30_000 });

            var content = await page.TextContentAsync("[data-testid='home-container']");
            Assert.Contains("LoreEngine", content ?? "");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BestiaryPage_ShowsCreatures()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/bestiary");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });

            // Bestiary should show creature names
            var content = await page.TextContentAsync("body");
            Assert.Contains("Goblin Scout", content ?? "");
            Assert.Contains("Ancient Dragon", content ?? "");
            Assert.Contains("Bestiary", content ?? "");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task NavMenu_ContainsAllLinks()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(fixture.ClientUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });

            var navContent = await page.TextContentAsync("nav");
            Assert.Contains("Play", navContent ?? "");
            Assert.Contains("Journal", navContent ?? "");
            Assert.Contains("Bestiary", navContent ?? "");
            Assert.Contains("Achievements", navContent ?? "");
            Assert.Contains("Chat", navContent ?? "");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task JournalPage_RequiresAuth()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/journal");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });

            // Should redirect to login or show error
            var content = await page.TextContentAsync("body");
            var url = page.Url;
            // Either redirected to login or shows an error about loading
            Assert.True(
                url.Contains("/login") || content?.Contains("login") == true || content?.Contains("Failed") == true,
                "Journal should require authentication");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task AchievementsPage_RequiresAuth()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/achievements");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });

            var content = await page.TextContentAsync("body");
            var url = page.Url;
            Assert.True(
                url.Contains("/login") || content?.Contains("login") == true || content?.Contains("Failed") == true,
                "Achievements should require authentication");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
