using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class ProxyChatClientTests
{
    private static ProxyChatClient CreateClient(
        Queue<Shared.Contracts.ChatResponse> serverResponses,
        StoryStateService? storyState = null)
    {
        storyState ??= new StoryStateService();
        var handler = new FakeHttpMessageHandler(serverResponses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        var registry = new ClientToolRegistry(
            new GetStoryGraphTool(storyState),
            new SearchStoryTool(storyState),
            new SaveStoryStateTool(storyState),
            new GetWorldRulesTool(storyState));
        return new ProxyChatClient(httpClient, registry);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsTextResponse()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();
        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Hello!" }],
        });

        var client = CreateClient(serverResponses);

        var result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Single(result.Messages);
        Assert.Equal("Hello!", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_ExecutesClientToolAndLoops()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();

        // First response: server returns a tool call for GetStoryGraph
        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto
            {
                Role = "assistant",
                ToolCalls = [new ToolCallDto
                {
                    CallId = "call-1",
                    Name = "GetStoryGraph",
                    Arguments = null,
                }],
            }],
        });

        // Second response: after tool execution, server returns text
        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "The story graph is empty." }],
        });

        var client = CreateClient(serverResponses);

        var result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Show me the story")]);

        Assert.Single(result.Messages);
        Assert.Equal("The story graph is empty.", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_ExecutesRollDiceTool()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();

        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto
            {
                Role = "assistant",
                ToolCalls = [new ToolCallDto
                {
                    CallId = "call-dice",
                    Name = "RollDice",
                    Arguments = JsonSerializer.SerializeToElement(
                        new Dictionary<string, object> { ["count"] = 2, ["sides"] = 6 }),
                }],
            }],
        });

        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "You rolled the dice!" }],
        });

        var client = CreateClient(serverResponses);

        var result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Roll dice")]);

        Assert.Equal("You rolled the dice!", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_RespectsMaxToolCallRounds()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();

        // Queue 6 tool call responses (more than the limit of 5)
        for (int i = 0; i < 6; i++)
        {
            serverResponses.Enqueue(new Shared.Contracts.ChatResponse
            {
                Messages = [new ChatMessageDto
                {
                    Role = "assistant",
                    ToolCalls = [new ToolCallDto
                    {
                        CallId = $"call-{i}",
                        Name = "GetStoryGraph",
                        Arguments = null,
                    }],
                }],
            });
        }

        var client = CreateClient(serverResponses);

        var result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Loop forever")]);

        Assert.Contains("limit reached", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_HandlesUnknownToolGracefully()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();

        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto
            {
                Role = "assistant",
                ToolCalls = [new ToolCallDto
                {
                    CallId = "call-unknown",
                    Name = "NonExistentTool",
                    Arguments = null,
                }],
            }],
        });

        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Tool not found." }],
        });

        var client = CreateClient(serverResponses);

        var result = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Call unknown")]);

        Assert.Equal("Tool not found.", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsText()
    {
        var serverResponses = new Queue<Shared.Contracts.ChatResponse>();
        serverResponses.Enqueue(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Streamed content" }],
        });

        var client = CreateClient(serverResponses);
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Stream")]))
        {
            updates.Add(update);
        }

        Assert.Single(updates);
        Assert.Equal("Streamed content", updates[0].Text);
    }

    /// <summary>
    /// Fake HttpMessageHandler that returns queued responses.
    /// </summary>
    private sealed class FakeHttpMessageHandler(Queue<Shared.Contracts.ChatResponse> responses) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Verify the request is for the chat endpoint
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            var response = responses.Dequeue();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response),
            };
        }
    }
}
