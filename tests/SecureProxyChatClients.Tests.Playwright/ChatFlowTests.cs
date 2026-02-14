using Microsoft.Playwright;
using SecureProxyChatClients.Tests.Playwright.Infrastructure;

namespace SecureProxyChatClients.Tests.Playwright;

[Collection("Aspire")]
public class ChatFlowTests(AspirePlaywrightFixture fixture)
{
    private async Task<IPage> LoginAndNavigateToChat()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{fixture.ClientUrl}/login");
        await page.WaitForSelectorAsync("[data-testid='login-form']", new() { Timeout = 30_000 });

        await page.Locator("[data-testid='login-email']").FillAsync("test@test.com");
        await page.Locator("[data-testid='login-password']").FillAsync("Test123!");
        await page.Locator("[data-testid='login-submit']").ClickAsync();

        await page.WaitForURLAsync("**/ping", new() { Timeout = 15_000 });
        await page.GotoAsync($"{fixture.ClientUrl}/chat");
        await page.WaitForSelectorAsync("[data-testid='chat-container']", new() { Timeout = 30_000 });

        return page;
    }

    [Fact]
    public async Task ChatPage_WithoutAuth_ShowsUnauthenticatedMessage()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.ClientUrl}/chat");
            await page.WaitForSelectorAsync("[data-testid='chat-unauthenticated']", new() { Timeout = 30_000 });

            var unauthDiv = page.Locator("[data-testid='chat-unauthenticated']");
            Assert.True(await unauthDiv.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task ChatPage_AfterLogin_ShowsChatInterface()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            var chatMessages = page.Locator("[data-testid='chat-messages']");
            var chatInput = page.Locator("[data-testid='chat-input']");
            var sendButton = page.Locator("[data-testid='chat-send']");

            Assert.True(await chatMessages.IsVisibleAsync());
            Assert.True(await chatInput.IsVisibleAsync());
            Assert.True(await sendButton.IsVisibleAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Chat_SendMessage_ReceivesResponse()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            await page.Locator("[data-testid='chat-input']").FillAsync("Hello world");
            await page.Locator("[data-testid='chat-send']").ClickAsync();

            // Wait for at least 2 messages (user + assistant)
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"chat-message-content\"]').length >= 2",
                null,
                new() { Timeout = 15_000 });

            var messageContents = page.Locator("[data-testid='chat-message-content']");
            var count = await messageContents.CountAsync();
            Assert.True(count >= 2, $"Expected at least 2 messages, got {count}");

            // Last message should be from assistant (fake response)
            var lastText = await messageContents.Last.TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(lastText), "Assistant response should not be empty");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Chat_StreamMessage_ReceivesStreamedResponse()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            await page.Locator("[data-testid='chat-input']").FillAsync("Tell me a story");
            await page.Locator("[data-testid='chat-send-stream']").ClickAsync();

            // Wait for streaming content or final message
            // Either streaming indicator appears or final response
            var streamingOrMessage = page.Locator("[data-testid='chat-streaming-content'], [data-testid='chat-message-content']");
            await streamingOrMessage.First.WaitForAsync(new() { Timeout = 15_000 });

            // Wait for streaming to complete — the assistant message should appear
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"chat-message-content\"]').length >= 2",
                null,
                new() { Timeout = 15_000 });

            var messageContents = page.Locator("[data-testid='chat-message-content']");
            var count = await messageContents.CountAsync();
            Assert.True(count >= 2, $"Expected at least 2 messages after streaming, got {count}");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Chat_MultiTurn_MaintainsHistory()
    {
        var page = await LoginAndNavigateToChat();
        try
        {
            // Send first message
            await page.Locator("[data-testid='chat-input']").FillAsync("First message");
            await page.Locator("[data-testid='chat-send']").ClickAsync();

            // Wait for response
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"chat-message-content\"]').length >= 2",
                null,
                new() { Timeout = 15_000 });

            // Send second message
            await page.Locator("[data-testid='chat-input']").FillAsync("Second message");
            await page.Locator("[data-testid='chat-send']").ClickAsync();

            // Wait for second response
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('[data-testid=\"chat-message-content\"]').length >= 4",
                null,
                new() { Timeout = 15_000 });

            var messageContents = page.Locator("[data-testid='chat-message-content']");
            var count = await messageContents.CountAsync();
            Assert.True(count >= 4, $"Expected at least 4 messages (2 turns), got {count}");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
