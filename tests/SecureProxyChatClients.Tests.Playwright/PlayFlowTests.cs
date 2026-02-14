using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class PlayFlowTests(AspirePlaywrightFixture fixture) : IAsyncLifetime
{
    private Microsoft.Playwright.IPage _page = null!;

    public async Task InitializeAsync()
    {
        _page = await fixture.Browser!.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
    }

    private async Task LoginAndGoToPlayAsync()
    {
        await _page.GotoAsync($"{fixture.ClientUrl}/login");
        await _page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

        await _page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
        await _page.Locator("[data-testid='login-password']").FillAsync("Test123!");
        await _page.Locator("[data-testid='login-submit']").ClickAsync();

        await _page.WaitForURLAsync("**/ping", new() { Timeout = 15_000 });
        await _page.GotoAsync($"{fixture.ClientUrl}/play");
    }

    private async Task EnsureCharacterCreationAsync()
    {
        // Wait for the page to finish loading (character creation, player stats, or loading spinner)
        try
        {
            await _page.WaitForSelectorAsync(
                "[data-testid='character-creation'], [data-testid='player-stats'], [data-testid='play-unauthenticated']",
                new() { Timeout = 30_000 });
        }
        catch { /* timeout — page might still be loading */ }

        // If a game is already running, reset it
        var newGameBtn = await _page.QuerySelectorAsync("[data-testid='new-game-btn']");
        if (newGameBtn is not null && await newGameBtn.IsVisibleAsync())
        {
            await newGameBtn.ClickAsync();
            // Give Blazor time to re-render
            await _page.WaitForTimeoutAsync(500);
        }

        await _page.WaitForSelectorAsync("[data-testid='character-creation']", new() { Timeout = 30_000 });
    }

    [Fact]
    public async Task Play_ShowsLoginPrompt_WhenUnauthenticated()
    {
        await _page.GotoAsync($"{fixture.ClientUrl}/play");
        await _page.WaitForSelectorAsync("[data-testid='play-unauthenticated']", new() { Timeout = 30_000 });

        var alert = await _page.TextContentAsync("[data-testid='play-unauthenticated']");
        Assert.Contains("login", alert, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Play_ShowsCharacterCreation_WhenAuthenticated()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        var nameInput = await _page.QuerySelectorAsync("[data-testid='character-name']");
        Assert.NotNull(nameInput);

        var classCards = await _page.QuerySelectorAllAsync("[data-testid^='class-']");
        Assert.Equal(4, classCards.Count);
    }

    [Fact]
    public async Task Play_StartButton_DisabledWithoutNameAndClass()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        var startBtn = _page.Locator("[data-testid='start-game']");
        Assert.True(await startBtn.IsDisabledAsync());
    }

    [Fact]
    public async Task Play_StartsGame_WithNameAndClass()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        await _page.FillAsync("[data-testid='character-name']", "TestHero");
        await _page.ClickAsync("[data-testid='class-warrior']");

        var startBtn = _page.Locator("[data-testid='start-game']");
        Assert.False(await startBtn.IsDisabledAsync());

        await _page.ClickAsync("[data-testid='start-game']");

        await _page.WaitForSelectorAsync("[data-testid='player-stats']", new() { Timeout = 15_000 });
        var stats = await _page.TextContentAsync("[data-testid='player-stats']");
        Assert.Contains("TestHero", stats);
    }

    [Fact]
    public async Task Play_GameUI_ShowsInventory()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        await _page.FillAsync("[data-testid='character-name']", "InventoryTester");
        await _page.ClickAsync("[data-testid='class-rogue']");
        await _page.ClickAsync("[data-testid='start-game']");

        await _page.WaitForSelectorAsync("[data-testid='inventory-sidebar']", new() { Timeout = 15_000 });
        var inventory = await _page.TextContentAsync("[data-testid='inventory-sidebar']");

        Assert.Contains("Daggers", inventory);
    }

    [Fact]
    public async Task Play_QuickActions_AreVisible()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        await _page.FillAsync("[data-testid='character-name']", "ActionTester");
        await _page.ClickAsync("[data-testid='class-explorer']");
        await _page.ClickAsync("[data-testid='start-game']");

        await _page.WaitForSelectorAsync("[data-testid='action-input']", new() { Timeout = 15_000 });

        var lookBtn = _page.Locator("button:has-text('Look')");
        Assert.True(await lookBtn.IsVisibleAsync());
    }

    [Fact]
    public async Task Play_NewGameButton_ResetsToCharacterCreation()
    {
        await LoginAndGoToPlayAsync();
        await EnsureCharacterCreationAsync();

        await _page.FillAsync("[data-testid='character-name']", "Resetter");
        await _page.ClickAsync("[data-testid='class-mage']");
        await _page.ClickAsync("[data-testid='start-game']");

        await _page.WaitForSelectorAsync("[data-testid='new-game-btn']", new() { Timeout = 15_000 });
        await _page.ClickAsync("[data-testid='new-game-btn']");

        await _page.WaitForSelectorAsync("[data-testid='character-creation']", new() { Timeout = 30_000 });
    }
}
