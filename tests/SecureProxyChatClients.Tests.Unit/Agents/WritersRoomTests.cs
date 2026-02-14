using Microsoft.Extensions.AI;
using SecureProxyChatClients.Client.Web.Agents;
using SecureProxyChatClients.Server.AI;

namespace SecureProxyChatClients.Tests.Unit.Agents;

public class WritersRoomTests
{
    [Fact]
    public async Task RunDiscussionAsync_AllAgentsRespond()
    {
        var fakeChatClient = new FakeChatClient();
        // 3 agents × 3 rounds = 9 responses needed
        for (int i = 0; i < 9; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        var messages = new List<AgentMessage>();
        await foreach (var msg in room.RunDiscussionAsync("A story about pirates"))
        {
            messages.Add(msg);
        }

        Assert.Equal(9, messages.Count);
    }

    [Fact]
    public async Task RunDiscussionAsync_RoundRobinOrder()
    {
        var fakeChatClient = new FakeChatClient();
        for (int i = 0; i < 9; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        var messages = new List<AgentMessage>();
        await foreach (var msg in room.RunDiscussionAsync("Test pitch"))
        {
            messages.Add(msg);
        }

        // Verify round-robin: Storyteller, Critic, Archivist, repeat
        for (int round = 0; round < 3; round++)
        {
            Assert.Equal("Storyteller", messages[round * 3 + 0].AgentName);
            Assert.Equal("Critic", messages[round * 3 + 1].AgentName);
            Assert.Equal("Archivist", messages[round * 3 + 2].AgentName);
        }
    }

    [Fact]
    public async Task RunDiscussionAsync_RespectsMaxRounds()
    {
        var fakeChatClient = new FakeChatClient();
        // Only 1 round × 3 agents = 3 responses
        for (int i = 0; i < 3; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        var messages = new List<AgentMessage>();
        await foreach (var msg in room.RunDiscussionAsync("Test", maxRounds: 1))
        {
            messages.Add(msg);
        }

        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public async Task RunDiscussionAsync_LastMessageIsFinal()
    {
        var fakeChatClient = new FakeChatClient();
        for (int i = 0; i < 6; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        var messages = new List<AgentMessage>();
        await foreach (var msg in room.RunDiscussionAsync("Test", maxRounds: 2))
        {
            messages.Add(msg);
        }

        Assert.Equal(6, messages.Count);
        // Only the very last message should be marked as final
        for (int i = 0; i < messages.Count - 1; i++)
        {
            Assert.False(messages[i].IsFinal, $"Message {i} should not be final");
        }
        Assert.True(messages[^1].IsFinal, "Last message should be final");
    }

    [Fact]
    public async Task RunDiscussionAsync_IncludesEmojis()
    {
        var fakeChatClient = new FakeChatClient();
        for (int i = 0; i < 3; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        var messages = new List<AgentMessage>();
        await foreach (var msg in room.RunDiscussionAsync("Test", maxRounds: 1))
        {
            messages.Add(msg);
        }

        Assert.Equal("📖", messages[0].AgentEmoji);
        Assert.Equal("🎭", messages[1].AgentEmoji);
        Assert.Equal("📚", messages[2].AgentEmoji);
    }

    [Fact]
    public async Task RunDiscussionAsync_SupportsCancellation()
    {
        var fakeChatClient = new FakeChatClient();
        for (int i = 0; i < 9; i++)
        {
            fakeChatClient.Responses.Enqueue(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}")));
        }

        var agents = LoreAgentFactory.CreateAll(fakeChatClient);
        var room = new WritersRoom(agents);

        using var cts = new CancellationTokenSource();
        var messages = new List<AgentMessage>();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var msg in room.RunDiscussionAsync("Test", maxRounds: 3, cts.Token))
            {
                messages.Add(msg);
                if (messages.Count == 2)
                    cts.Cancel();
            }
        });

        Assert.True(messages.Count >= 2 && messages.Count < 9);
    }
}
