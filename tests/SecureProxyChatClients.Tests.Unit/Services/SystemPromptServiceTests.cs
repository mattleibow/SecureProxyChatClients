using Microsoft.Extensions.Configuration;
using SecureProxyChatClients.Server.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Services;

public class SystemPromptServiceTests
{
    [Fact]
    public void PrependSystemPrompt_AddsSystemMessageFirst()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SystemPromptService(config);
        var messages = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "Hello" },
        };

        var result = service.PrependSystemPrompt(messages);

        Assert.Equal(2, result.Count);
        Assert.Equal("system", result[0].Role);
        Assert.Equal("user", result[1].Role);
    }

    [Fact]
    public void PrependSystemPrompt_UsesDefaultWhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SystemPromptService(config);
        var messages = new List<ChatMessageDto> { new() { Role = "user", Content = "test" } };

        var result = service.PrependSystemPrompt(messages);

        Assert.Contains("LoreEngine", result[0].Content);
    }

    [Fact]
    public void PrependSystemPrompt_UsesConfiguredPrompt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:SystemPrompt"] = "You are a pirate assistant.",
            })
            .Build();
        var service = new SystemPromptService(config);
        var messages = new List<ChatMessageDto> { new() { Role = "user", Content = "test" } };

        var result = service.PrependSystemPrompt(messages);

        Assert.Equal("You are a pirate assistant.", result[0].Content);
    }

    [Fact]
    public void PrependSystemPrompt_PreservesOriginalMessages()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SystemPromptService(config);
        var messages = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "Hello" },
            new() { Role = "assistant", Content = "Hi" },
            new() { Role = "user", Content = "How are you?" },
        };

        var result = service.PrependSystemPrompt(messages);

        Assert.Equal(4, result.Count);
        Assert.Equal("Hello", result[1].Content);
        Assert.Equal("Hi", result[2].Content);
        Assert.Equal("How are you?", result[3].Content);
    }
}
