using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.AI;

namespace SecureProxyChatClients.Tests.Unit.AI;

public class ObservabilityChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_DelegatesToInnerClient()
    {
        var inner = new FakeChatClient();
        inner.Responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello from inner")));
        var sut = new ObservabilityChatClient(inner);

        var messages = new List<ChatMessage> { new(ChatRole.User, "Hi") };
        var response = await sut.GetResponseAsync(messages);

        Assert.NotNull(response);
        Assert.Contains(response.Messages, m => m.Text?.Contains("Hello from inner") == true);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DelegatesToInnerClient()
    {
        var inner = new FakeChatClient();
        inner.Responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Streamed response")));
        var sut = new ObservabilityChatClient(inner);

        var messages = new List<ChatMessage> { new(ChatRole.User, "Hi") };
        var chunks = new List<string>();

        await foreach (var update in sut.GetStreamingResponseAsync(messages))
        {
            if (update.Text is { Length: > 0 })
                chunks.Add(update.Text);
        }

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsResponse_WhenNoDataEnqueued()
    {
        var inner = new FakeChatClient();
        var sut = new ObservabilityChatClient(inner);

        var messages = new List<ChatMessage> { new(ChatRole.User, "Hi") };
        var response = await sut.GetResponseAsync(messages);

        Assert.NotNull(response);
    }
}
