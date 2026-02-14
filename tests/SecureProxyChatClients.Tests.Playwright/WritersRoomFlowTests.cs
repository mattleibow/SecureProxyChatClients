using Microsoft.Playwright;
using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class WritersRoomFlowTests(AspirePlaywrightFixture fixture)
{
    private async Task<IPage> LoginAndNavigateToWritersRoom()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{fixture.ClientUrl}/login");
        await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 60_000 });

        await page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
        await page.Locator("[data-testid='login-password']").FillAsync("Test123!");
        await page.Locator("[data-testid='login-submit']").ClickAsync();

        await page.WaitForURLAsync("**/ping", new() { Timeout = 30_000 });
        await page.GotoAsync($"{fixture.ClientUrl}/writers-room");
        await page.WaitForSelectorAsync("[data-testid='writers-room-container']", new() { Timeout = 60_000 });

        return page;
    }

    [Fact]
    public async Task WritersRoom_WithoutAuth_ShowsUnauthenticatedMessage()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/writers-room");
            await page.WaitForSelectorAsync("[data-testid='writers-room-unauthenticated']", new() { Timeout = 30_000 });

            var unauthDiv = page.Locator("[data-testid='writers-room-unauthenticated']");
            Assert.True(await unauthDiv.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task WritersRoom_AfterLogin_ShowsPitchForm()
    {
        var page = await LoginAndNavigateToWritersRoom();
        try
        {
            var pitchInput = page.Locator("[data-testid='pitch-input']");
            var submitButton = page.Locator("[data-testid='pitch-submit']");

            Assert.True(await pitchInput.IsVisibleAsync());
            Assert.True(await submitButton.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task WritersRoom_SubmitPitch_ShowsAgentResponses()
    {
        var page = await LoginAndNavigateToWritersRoom();
        try
        {
            await page.Locator("[data-testid='pitch-input']").FillAsync("A noir detective story set in 1940s Chicago");
            await page.Locator("[data-testid='pitch-submit']").ClickAsync();

            // Wait for agent messages to appear (at least 3 — one per agent in round 1)
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"agent-message\"]').length >= 3",
                null,
                new() { Timeout = 60_000 });

            var agentMessages = page.Locator("[data-testid='agent-message']");
            var count = await agentMessages.CountAsync();
            Assert.True(count >= 3, $"Expected at least 3 agent messages, got {count}");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task WritersRoom_SubmitPitch_ShowsAgentBadges()
    {
        var page = await LoginAndNavigateToWritersRoom();
        try
        {
            await page.Locator("[data-testid='pitch-input']").FillAsync("A space opera with sentient ships");
            await page.Locator("[data-testid='pitch-submit']").ClickAsync();

            // Wait for all 3 agent badges to appear
            await page.WaitForSelectorAsync("[data-testid='agent-badge-Storyteller']", new() { Timeout = 60_000 });
            await page.WaitForSelectorAsync("[data-testid='agent-badge-Critic']", new() { Timeout = 15_000 });
            await page.WaitForSelectorAsync("[data-testid='agent-badge-Archivist']", new() { Timeout = 15_000 });

            Assert.True(await page.Locator("[data-testid='agent-badge-Storyteller']").First.IsVisibleAsync());
            Assert.True(await page.Locator("[data-testid='agent-badge-Critic']").First.IsVisibleAsync());
            Assert.True(await page.Locator("[data-testid='agent-badge-Archivist']").First.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task WritersRoom_AllThreeAgentsRespond()
    {
        var page = await LoginAndNavigateToWritersRoom();
        try
        {
            await page.Locator("[data-testid='pitch-input']").FillAsync("A mystery in a haunted library");
            await page.Locator("[data-testid='pitch-submit']").ClickAsync();

            // Wait for discussion to complete (all 3 agents respond)
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"agent-message\"]').length >= 3",
                null,
                new() { Timeout = 90_000 });

            // Verify all 3 agent types responded
            var storytellerBadges = page.Locator("[data-testid='agent-badge-Storyteller']");
            var criticBadges = page.Locator("[data-testid='agent-badge-Critic']");
            var archivistBadges = page.Locator("[data-testid='agent-badge-Archivist']");

            Assert.True(await storytellerBadges.CountAsync() >= 1, "Storyteller should have at least 1 response");
            Assert.True(await criticBadges.CountAsync() >= 1, "Critic should have at least 1 response");
            Assert.True(await archivistBadges.CountAsync() >= 1, "Archivist should have at least 1 response");

            // Verify message content is not empty
            var messageContents = page.Locator("[data-testid='agent-message-content']");
            var contentCount = await messageContents.CountAsync();
            for (int i = 0; i < contentCount; i++)
            {
                var text = await messageContents.Nth(i).TextContentAsync();
                Assert.False(string.IsNullOrWhiteSpace(text), $"Agent message {i} should not be empty");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
