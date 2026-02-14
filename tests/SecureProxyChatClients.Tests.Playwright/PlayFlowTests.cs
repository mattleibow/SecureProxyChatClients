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
        await _page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 60_000 });

        await _page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
        await _page.Locator("[data-testid='login-password']").FillAsync("Test123!");
        await _page.Locator("[data-testid='login-submit']").ClickAsync();

        await _page.WaitForURLAsync("**/ping", new() { Timeout = 30_000 });
        await _page.GotoAsync($"{fixture.ClientUrl}/play");
    }

    private async Task EnsureCharacterCreationAsync()
    {
        // Wait for the play page to be fully ready
        // The page may transition through: loading → character-creation or player-stats
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _page.WaitForSelectorAsync(
                    "[data-testid='character-creation'], [data-testid='player-stats']",
                    new() { Timeout = 30_000 });
                break;
            }
            catch (TimeoutException) when (attempt < 2)
            {
                // Page might be in a transient state; reload and retry
                await _page.ReloadAsync();
            }
        }

        // If a game is already running, reset it
        var hasPlayerStats = await _page.Locator("[data-testid='player-stats']").IsVisibleAsync();
        if (hasPlayerStats)
        {
            var newGameBtn = _page.Locator("[data-testid='new-game-btn']");
            await newGameBtn.WaitForAsync(new() { Timeout = 10_000 });
            await newGameBtn.ClickAsync();
            await _page.WaitForSelectorAsync("[data-testid='character-creation']", new() { Timeout = 30_000 });
        }
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

        await _page.WaitForSelectorAsync("[data-testid='player-stats']", new() { Timeout = 60_000 });        var stats = await _page.TextContentAsync("[data-testid='player-stats']");
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

        await _page.WaitForSelectorAsync("[data-testid='player-stats']", new() { Timeout = 60_000 });
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

        await _page.WaitForSelectorAsync("[data-testid='action-input']", new() { Timeout = 60_000 });

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

        // Wait for the game UI to appear and streaming to settle
        await _page.WaitForSelectorAsync("[data-testid='player-stats']", new() { Timeout = 60_000 });
        await _page.WaitForTimeoutAsync(2000);
        
        await _page.ClickAsync("[data-testid='new-game-btn']");

        await _page.WaitForSelectorAsync("[data-testid='character-creation']", new() { Timeout = 30_000 });
    }
}
