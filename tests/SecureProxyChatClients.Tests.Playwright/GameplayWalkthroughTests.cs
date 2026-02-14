using Microsoft.Playwright;

namespace SecureProxyChatClients.Tests.Playwright;

/// <summary>
/// Full gameplay walkthrough with real AI. Requires server on :5167 and client on :5053.
/// Captures screenshots to docs/game/screenshots/ for the walkthrough documentation.
/// Run manually: dotnet test --filter "FullyQualifiedName~GameplayWalkthrough"
/// </summary>
[Trait("Category", "Walkthrough")]
public class GameplayWalkthroughTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private string _screenshotDir = null!;
    private int _screenshotIndex;
    private readonly List<string> _storyLog = new();

    private const string ClientUrl = "http://localhost:5053";
    private const string ServerUrl = "http://localhost:5167";

    public async Task InitializeAsync()
    {
        // Find repo root (walk up from test assembly)
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Directory.GetParent(dir)?.FullName;
        var repoRoot = dir ?? throw new InvalidOperationException("Could not find repo root");
        _screenshotDir = Path.Combine(repoRoot, "docs", "game", "screenshots");
        Directory.CreateDirectory(_screenshotDir);

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            SlowMo = 200
        });
        _page = await _browser.NewPageAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        });
    }

    public async Task DisposeAsync()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private async Task Screenshot(string name, string description)
    {
        _screenshotIndex++;
        var filename = $"{_screenshotIndex:D2}-{name}.png";
        var path = Path.Combine(_screenshotDir, filename);
        await _page.ScreenshotAsync(new() { Path = path, FullPage = false });
        _storyLog.Add($"## {_screenshotIndex}. {description}\n![{description}]({filename})\n");
    }

    private async Task WaitForStreamingResponse(int timeoutMs = 60_000)
    {
        // Let streaming start
        await _page.WaitForTimeoutAsync(2000);
        try
        {
            // Wait for the action input to be re-enabled (streaming complete)
            await _page.WaitForSelectorAsync(
                "[data-testid='action-text']:not([disabled])",
                new() { Timeout = timeoutMs });
        }
        catch
        {
            // Timeout is acceptable — AI may be slow or element may not exist yet
        }

        // Extra settle time for rendering
        await _page.WaitForTimeoutAsync(1500);
    }

    private async Task TypeActionAndSend(string text)
    {
        var input = _page.Locator("[data-testid='action-text']");
        await input.WaitForAsync(new() { Timeout = 30_000 });
        await input.FillAsync(text);
        await _page.WaitForTimeoutAsync(300);
        var sendBtn = _page.Locator("[data-testid='send-action']");
        await sendBtn.ClickAsync();
    }

    private async Task ClickQuickAction(string buttonText)
    {
        var btn = _page.Locator($"button:has-text('{buttonText}')").First;
        await btn.WaitForAsync(new() { Timeout = 30_000 });
        await btn.ClickAsync();
    }

    private async Task NavigateViaNavLink(string href, int waitMs = 8000)
    {
        var link = _page.Locator($"a[href='{href}']").First;
        await link.WaitForAsync(new() { Timeout = 15_000 });
        await link.ClickAsync();
        await _page.WaitForTimeoutAsync(waitMs);
    }

    [Fact]
    public async Task FullGameplay_AllMechanics()
    {
        var errors = new List<string>();

        // === Step 1: Registration ===
        try
        {
            await _page.GotoAsync($"{ClientUrl}/register", new() { WaitUntil = WaitUntilState.NetworkIdle });
            // WASM takes 30-60s to load on first visit
            await _page.WaitForSelectorAsync("[data-testid='register-form']", new() { Timeout = 90_000 });

            var uniqueEmail = $"walkthrough+{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}@test.com";
            var password = "DragonSlayer42!";

            await _page.FillAsync("[data-testid='register-email']", uniqueEmail);
            await _page.FillAsync("[data-testid='register-password']", password);
            await _page.FillAsync("[data-testid='register-confirm']", password);
            await Screenshot("registration-form", "Registration — Filled out the signup form");

            await _page.ClickAsync("[data-testid='register-submit']");
            // Registration redirects to /play via SPA navigation (WASM stays loaded)
            await _page.WaitForURLAsync("**/play", new() { Timeout = 15_000 });
            await _page.WaitForTimeoutAsync(3000);
            await Screenshot("registration-complete", "Registration — Account created, redirected to Play");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 1 (Registration): {ex.Message}");
            await Screenshot("registration-error", "Registration — Error encountered");
        }

        // === Step 2: Character Creation ===
        try
        {
            // After registration we're already on /play via SPA nav (no page reload needed)
            await _page.WaitForSelectorAsync("[data-testid='character-creation']", new() { Timeout = 30_000 });

            await _page.FillAsync("[data-testid='character-name']", "Kael Stormborn");
            await _page.ClickAsync("[data-testid='class-warrior']");
            await _page.WaitForTimeoutAsync(500);
            await Screenshot("character-creation", "Character Creation — Named Kael Stormborn, selected Warrior class");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 2 (Character Creation): {ex.Message}");
            await Screenshot("character-creation-error", "Character Creation — Error encountered");
        }

        // === Step 3: Begin Adventure ===
        try
        {
            await _page.ClickAsync("[data-testid='start-game']");
            await WaitForStreamingResponse();
            await Screenshot("opening-scene", "Begin Adventure — The opening scene unfolds");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 3 (Begin Adventure): {ex.Message}");
            await Screenshot("begin-adventure-error", "Begin Adventure — Error encountered");
        }

        // === Step 4: Look Around ===
        try
        {
            await ClickQuickAction("Look");
            await WaitForStreamingResponse();
            await Screenshot("look-around", "Look Around — Surveying the surroundings");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 4 (Look Around): {ex.Message}");
            await Screenshot("look-around-error", "Look Around — Error encountered");
        }

        // === Step 5: Move to Dark Forest ===
        try
        {
            await TypeActionAndSend("I travel to the Dark Forest");
            await WaitForStreamingResponse();
            await Screenshot("dark-forest", "Dark Forest — Journeying into the unknown");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 5 (Dark Forest): {ex.Message}");
            await Screenshot("dark-forest-error", "Dark Forest — Error encountered");
        }

        // === Step 6: World Map ===
        try
        {
            await ClickQuickAction("Map");
            await WaitForStreamingResponse();
            await Screenshot("world-map", "World Map — Viewing the realm");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 6 (World Map): {ex.Message}");
            await Screenshot("world-map-error", "World Map — Error encountered");
        }

        // === Step 7: Search ===
        try
        {
            await ClickQuickAction("Search");
            await WaitForStreamingResponse();
            await Screenshot("search-area", "Search — Scouring the area for hidden items");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 7 (Search): {ex.Message}");
            await Screenshot("search-error", "Search — Error encountered");
        }

        // === Step 8: Encounter ===
        try
        {
            await ClickQuickAction("Fight");
            await WaitForStreamingResponse();
            await Screenshot("encounter", "Encounter — A wild creature appears!");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 8 (Encounter): {ex.Message}");
            await Screenshot("encounter-error", "Encounter — Error encountered");
        }

        // === Step 9: Attack ===
        try
        {
            await TypeActionAndSend("I attack with my Iron Sword!");
            await WaitForStreamingResponse();
            await Screenshot("attack", "Attack — Swinging the Iron Sword");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 9 (Attack): {ex.Message}");
            await Screenshot("attack-error", "Attack — Error encountered");
        }

        // === Step 10: Defend ===
        try
        {
            await TypeActionAndSend("I raise my shield to defend");
            await WaitForStreamingResponse();
            await Screenshot("defend", "Defend — Raising the shield");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 10 (Defend): {ex.Message}");
            await Screenshot("defend-error", "Defend — Error encountered");
        }

        // === Step 11: Use Potion ===
        try
        {
            await TypeActionAndSend("I use a Healing Potion");
            await WaitForStreamingResponse();
            await Screenshot("use-potion", "Use Potion — Drinking a Healing Potion");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 11 (Use Potion): {ex.Message}");
            await Screenshot("use-potion-error", "Use Potion — Error encountered");
        }

        // === Step 12: Twist of Fate ===
        try
        {
            await ClickQuickAction("Twist");
            await WaitForStreamingResponse();
            await Screenshot("twist-of-fate", "Twist of Fate — Something unexpected happens!");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 12 (Twist of Fate): {ex.Message}");
            await Screenshot("twist-error", "Twist of Fate — Error encountered");
        }

        // === Step 13: Oracle ===
        try
        {
            await ClickQuickAction("Oracle");
            await WaitForStreamingResponse();
            await Screenshot("oracle", "Oracle — Consulting the Oracle for guidance");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 13 (Oracle): {ex.Message}");
            await Screenshot("oracle-error", "Oracle — Error encountered");
        }

        // === Step 14: Rest ===
        try
        {
            await ClickQuickAction("Rest");
            await WaitForStreamingResponse();
            await Screenshot("rest", "Rest — Tending to wounds and recovering");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 14 (Rest): {ex.Message}");
            await Screenshot("rest-error", "Rest — Error encountered");
        }

        // === Step 15: Talk to NPC ===
        try
        {
            await ClickQuickAction("Talk");
            await WaitForStreamingResponse();
            await Screenshot("talk-npc", "Talk to NPC — Speaking with a nearby character");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 15 (Talk to NPC): {ex.Message}");
            await Screenshot("talk-npc-error", "Talk to NPC — Error encountered");
        }

        // === Step 16: Move to Village ===
        try
        {
            await TypeActionAndSend("I travel to the Village of Thornwall");
            await WaitForStreamingResponse();
            await Screenshot("village", "Village — Arriving at the Village of Thornwall");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 16 (Village): {ex.Message}");
            await Screenshot("village-error", "Village — Error encountered");
        }

        // === Step 17: Explore Market ===
        try
        {
            await TypeActionAndSend("I visit the Market Square to buy supplies");
            await WaitForStreamingResponse();
            await Screenshot("market", "Market — Browsing the Market Square for supplies");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 17 (Market): {ex.Message}");
            await Screenshot("market-error", "Market — Error encountered");
        }

        // === Step 18: Bestiary Page ===
        try
        {
            await NavigateViaNavLink("bestiary");
            await Screenshot("bestiary", "Bestiary — Viewing the creature compendium");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 18 (Bestiary): {ex.Message}");
            await Screenshot("bestiary-error", "Bestiary — Error encountered");
        }

        // === Step 19: Journal Page ===
        try
        {
            await NavigateViaNavLink("journal");
            await Screenshot("journal", "Journal — Reviewing the adventure journal");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 19 (Journal): {ex.Message}");
            await Screenshot("journal-error", "Journal — Error encountered");
        }

        // === Step 20: Achievements Page ===
        try
        {
            await NavigateViaNavLink("achievements");
            await Screenshot("achievements", "Achievements — Checking earned achievements");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 20 (Achievements): {ex.Message}");
            await Screenshot("achievements-error", "Achievements — Error encountered");
        }

        // === Step 21: Create Story Page ===
        try
        {
            await NavigateViaNavLink("create-story");
            await _page.WaitForTimeoutAsync(3000);
            await Screenshot("create-story", "Create Story — The story creation wizard");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 21 (Create Story): {ex.Message}");
            await Screenshot("create-story-error", "Create Story — Error encountered");
        }

        // === Step 22: Writers Room Page ===
        try
        {
            await NavigateViaNavLink("writers-room");
            await _page.WaitForTimeoutAsync(3000);

            var pitchInput = _page.Locator("[data-testid='pitch-input']");
            // The pitch form may need a new discussion first
            try
            {
                var newDiscussionBtn = _page.Locator("[data-testid='new-discussion-btn']");
                if (await newDiscussionBtn.IsVisibleAsync())
                    await newDiscussionBtn.ClickAsync();
                await _page.WaitForTimeoutAsync(1000);
            }
            catch { /* May not need to click new discussion */ }

            await pitchInput.FillAsync("A warrior's journey through a cursed forest where the trees whisper forgotten prophecies");
            await Screenshot("writers-room-pitch", "Writers Room — Pitching a story idea");

            await _page.ClickAsync("[data-testid='pitch-submit']");
            await _page.WaitForTimeoutAsync(15_000); // AI agents discussing takes time
            await Screenshot("writers-room-discussion", "Writers Room — AI agents discussing the pitch");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 22 (Writers Room): {ex.Message}");
            await Screenshot("writers-room-error", "Writers Room — Error encountered");
        }

        // === Step 23: Chat Page ===
        try
        {
            await NavigateViaNavLink("chat");
            await _page.WaitForTimeoutAsync(3000);

            await _page.FillAsync("[data-testid='chat-input']", "Tell me about the world of this game");
            await Screenshot("chat-input", "Chat — Typing a message");

            await _page.ClickAsync("[data-testid='chat-send']");
            // Wait for streaming chat response
            await _page.WaitForTimeoutAsync(2000);
            try
            {
                await _page.WaitForSelectorAsync(
                    "[data-testid='chat-send']:not([disabled])",
                    new() { Timeout = 60_000 });
            }
            catch { /* timeout OK */ }
            await _page.WaitForTimeoutAsync(1500);
            await Screenshot("chat-response", "Chat — AI response received");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 23 (Chat): {ex.Message}");
            await Screenshot("chat-error", "Chat — Error encountered");
        }

        // === Step 24: Home Page (Authenticated) ===
        try
        {
            await NavigateViaNavLink("", 5000); // href="" is the home link
            await Screenshot("home-authenticated", "Home Page — Authenticated view of the landing page");
        }
        catch (Exception ex)
        {
            errors.Add($"Step 24 (Home Page): {ex.Message}");
            await Screenshot("home-error", "Home Page — Error encountered");
        }

        // === Step 25: Save Story Log ===
        var logContent = "# 🎮 Gameplay Walkthrough\n\n"
            + $"*Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*\n\n"
            + string.Join("\n", _storyLog);

        if (errors.Count > 0)
        {
            logContent += "\n\n---\n\n## ⚠️ Errors Encountered\n\n";
            foreach (var err in errors)
                logContent += $"- {err}\n";
        }

        var logPath = Path.Combine(_screenshotDir, "walkthrough-log.md");
        await File.WriteAllTextAsync(logPath, logContent);

        // Report any errors but don't fail the entire test — the screenshots are the value
        if (errors.Count > 0)
        {
            // Log errors but still pass — the walkthrough captured what it could
            Console.WriteLine($"Walkthrough completed with {errors.Count} error(s):");
            foreach (var err in errors)
                Console.WriteLine($"  - {err}");
        }
    }
}
