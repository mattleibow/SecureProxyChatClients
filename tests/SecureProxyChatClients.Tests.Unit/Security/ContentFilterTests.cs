using Microsoft.Extensions.Logging.Abstractions;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Security;

public class ContentFilterTests
{
    private readonly ContentFilter _filter = new(NullLogger<ContentFilter>.Instance);

    [Fact]
    public void FilterResponse_PreservesSafeContent()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Hello world" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.Equal("Hello world", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_HandlesMultipleMessages()
    {
        var response = new ChatResponse
        {
            Messages =
            [
                new ChatMessageDto { Role = "assistant", Content = "Part 1" },
                new ChatMessageDto { Role = "assistant", Content = "Part 2" },
            ]
        };

        var result = _filter.FilterResponse(response);
        Assert.Equal(2, result.Messages.Count);
    }

    [Fact]
    public void FilterResponse_RemovesScriptTags()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Hello <script>alert('xss')</script> world" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.Equal("Hello [content removed] world", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_RemovesIframeTags()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Check <iframe src='evil.com'></iframe> this" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.Equal("Check [content removed] this", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_RemovesEventHandlers()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = """<div onclick="alert('xss')">Click</div>""" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.DoesNotContain("onclick", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_RemovesJavascriptProtocol()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = """<a href="javascript:alert('xss')">link</a>""" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.DoesNotContain("javascript:", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_PreservesCodeBlocks()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "```ascii\n  /\\_/\\\n ( o.o )\n  > ^ <\n```" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.Contains("ascii", result.Messages[0].Content);
        Assert.Contains("/\\_/\\", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_HandlesSingleQuotedEventHandlers()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "<img onerror='alert(1)'>" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.DoesNotContain("onerror", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_HandlesUnquotedEventHandlers()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "<img onerror=alert(1)>" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.DoesNotContain("onerror", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_HandlesCaseInsensitiveScripts()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Test <SCRIPT>evil()</SCRIPT> end" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.Equal("Test [content removed] end", result.Messages[0].Content);
    }

    [Fact]
    public void FilterResponse_CatchesSplitScriptTagInConcatenatedText()
    {
        // Simulates the stream-split XSS scenario: malicious content split across chunks
        // Individual chunks may pass filtering, but concatenated text must be re-filtered
        string chunk1 = "Hello <scr";
        string chunk2 = "ipt>alert('xss')</scr";
        string chunk3 = "ipt> world";

        // Filter each chunk individually (as happens during streaming)
        var filtered1 = _filter.FilterResponse(new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = chunk1 }]
        }).Messages[0].Content;

        var filtered2 = _filter.FilterResponse(new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = chunk2 }]
        }).Messages[0].Content;

        var filtered3 = _filter.FilterResponse(new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = chunk3 }]
        }).Messages[0].Content;

        // Concatenate and apply final filter (as the server now does before persistence)
        string concatenated = filtered1 + filtered2 + filtered3;
        var finalResult = _filter.FilterResponse(new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = concatenated }]
        });

        // The final filter must catch the reassembled script tag
        Assert.DoesNotContain("<script>", finalResult.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterResponse_RemovesObjectAndEmbedTags()
    {
        var response = new ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "<object data='evil.swf'></object><embed src='evil.swf'>" }]
        };

        var result = _filter.FilterResponse(response);

        Assert.DoesNotContain("<object", result.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", result.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }
}
