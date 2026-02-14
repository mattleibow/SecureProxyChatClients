using Microsoft.Extensions.Logging.Abstractions;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Security;

public class ContentFilterTests
{
    [Fact]
    public void FilterResponse_PassesThrough()
    {
        var filter = new ContentFilter(NullLogger<ContentFilter>.Instance);
        var response = new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Hello world" }]
        };

        Shared.Contracts.ChatResponse result = filter.FilterResponse(response);

        Assert.Same(response, result);
        Assert.Equal("Hello world", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_HandlesMultipleMessages()
    {
        var filter = new ContentFilter(NullLogger<ContentFilter>.Instance);
        var response = new Shared.Contracts.ChatResponse
        {
            Messages =
            [
                new ChatMessageDto { Role = "assistant", Content = "Part 1" },
                new ChatMessageDto { Role = "assistant", Content = "Part 2" },
            ]
        };

        Shared.Contracts.ChatResponse result = filter.FilterResponse(response);

        Assert.Equal(2, result.Messages.Count);
    }
}
