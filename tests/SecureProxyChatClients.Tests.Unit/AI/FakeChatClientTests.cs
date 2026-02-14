using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.AI;

namespace SecureProxyChatClients.Tests.Unit.AI;

public class FakeChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_DequeuesResponse()
    {
        var client = new FakeChatClient();
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"));
        client.Responses.Enqueue(expected);

        ChatResponse result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Same(expected, result);
        Assert.Single(client.ReceivedMessages);
    }

    [Fact]
    public async Task GetResponseAsync_DequeuesInOrder()
    {
        var client = new FakeChatClient();
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, "First"));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Second"));
        client.Responses.Enqueue(first);
        client.Responses.Enqueue(second);

        ChatResponse result1 = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "1")]);
        ChatResponse result2 = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "2")]);

        Assert.Same(first, result1);
        Assert.Same(second, result2);
        Assert.Equal(2, client.ReceivedMessages.Count);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsDefaultWhenQueueEmpty()
    {
        var client = new FakeChatClient();

        ChatResponse result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Equal("This is a fake response.", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsUpdates()
    {
        var client = new FakeChatClient();
        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, "Hello "),
            new(ChatRole.Assistant, "World"),
        };
        client.StreamingResponses.Enqueue(updates);

        List<ChatResponseUpdate> results = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")]))
        {
            results.Add(update);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal("Hello ", results[0].Text);
        Assert.Equal("World", results[1].Text);
    }

    [Fact]
    public async Task ReceivedMessages_TracksAllCalls()
    {
        var client = new FakeChatClient();
        client.Responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, "R1")));
        client.Responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, "R2")));

        var msg1 = new List<ChatMessage> { new(ChatRole.User, "Q1") };
        var msg2 = new List<ChatMessage> { new(ChatRole.User, "Q2") };

        await client.GetResponseAsync(msg1);
        await client.GetResponseAsync(msg2);

        Assert.Equal(2, client.ReceivedMessages.Count);
    }
}
