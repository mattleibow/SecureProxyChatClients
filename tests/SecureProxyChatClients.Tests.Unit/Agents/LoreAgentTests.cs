using Microsoft.Extensions.AI;
using SecureProxyChatClients.Client.Web.Agents;
using SecureProxyChatClients.Server.AI;

namespace SecureProxyChatClients.Tests.Unit.Agents;

public class LoreAgentTests
{
    [Fact]
    public async Task RespondAsync_PrependsSystemPrompt()
    {
        var fakeChatClient = new FakeChatClient();
        fakeChatClient.Responses.Enqueue(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Once upon a time...")));

        var agent = new LoreAgent("Storyteller", "📖", "You are the Storyteller.", fakeChatClient);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Tell me a story about dragons"),
        };

        string response = await agent.RespondAsync(history);

        Assert.Equal("Once upon a time...", response);
        Assert.Single(fakeChatClient.ReceivedMessages);

        var sentMessages = fakeChatClient.ReceivedMessages[0].ToList();
        Assert.Equal(2, sentMessages.Count);
        Assert.Equal(ChatRole.System, sentMessages[0].Role);
        Assert.Equal("You are the Storyteller.", sentMessages[0].Text);
        Assert.Equal(ChatRole.User, sentMessages[1].Role);
    }

    [Fact]
    public async Task RespondAsync_ReturnsEmptyString_WhenNoAssistantText()
    {
        var fakeChatClient = new FakeChatClient();
        fakeChatClient.Responses.Enqueue(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));

        var agent = new LoreAgent("Critic", "🎭", "You are the Critic.", fakeChatClient);

        string response = await agent.RespondAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Equal(string.Empty, response);
    }

    [Fact]
    public async Task RespondAsync_PreservesConversationHistory()
    {
        var fakeChatClient = new FakeChatClient();
        fakeChatClient.Responses.Enqueue(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Noted.")));

        var agent = new LoreAgent("Archivist", "📚", "You are the Archivist.", fakeChatClient);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Start a story"),
            new(ChatRole.Assistant, "[Storyteller]: Once upon a time..."),
            new(ChatRole.Assistant, "[Critic]: That's too cliché."),
        };

        await agent.RespondAsync(history);

        var sentMessages = fakeChatClient.ReceivedMessages[0].ToList();
        Assert.Equal(4, sentMessages.Count); // system + 3 history
        Assert.Equal(ChatRole.System, sentMessages[0].Role);
    }

    [Fact]
    public void Factory_CreatesAllThreeAgents()
    {
        var fakeChatClient = new FakeChatClient();
        var agents = LoreAgentFactory.CreateAll(fakeChatClient);

        Assert.Equal(3, agents.Count);
        Assert.Equal("Storyteller", agents[0].Name);
        Assert.Equal("📖", agents[0].Emoji);
        Assert.Equal("Critic", agents[1].Name);
        Assert.Equal("🎭", agents[1].Emoji);
        Assert.Equal("Archivist", agents[2].Name);
        Assert.Equal("📚", agents[2].Emoji);
    }
}
