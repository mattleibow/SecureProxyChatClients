using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Client.Web.Tools;
using SecureProxyChatClients.Shared.Contracts;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace SecureProxyChatClients.Client.Web.Services;

/// <summary>
/// IChatClient that routes messages through the server proxy and handles
/// client-side tool execution transparently.
/// </summary>
public sealed class ProxyChatClient(HttpClient httpClient, ClientToolRegistry clientTools) : IChatClient
{
    private const int MaxToolCallRounds = 5;

    public async Task<AIChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessageDto> dtoMessages = ConvertToDto(messages);

        for (int round = 0; round < MaxToolCallRounds; round++)
        {
            var request = new ChatRequest
            {
                Messages = dtoMessages,
                ClientTools = BuildClientToolSchemas(),
            };

            using var response = await httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var chatResponse = await response.Content.ReadFromJsonAsync<Shared.Contracts.ChatResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Null response from server.");

            // Check for client tool calls in the response
            var toolCallMessages = chatResponse.Messages
                .Where(m => m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
                .ToList();

            if (toolCallMessages.Count == 0)
            {
                return ConvertFromDto(chatResponse);
            }

            // Execute client tool calls locally
            foreach (var assistantMsg in toolCallMessages)
            {
                dtoMessages.Add(assistantMsg);

                foreach (var toolCall in assistantMsg.ToolCalls!)
                {
                    AIFunction? tool = clientTools.GetTool(toolCall.Name);
                    string resultJson;

                    if (tool is not null)
                    {
                        var args = toolCall.Arguments is { } a
                            ? new AIFunctionArguments(
                                JsonSerializer.Deserialize<Dictionary<string, object?>>(a.GetRawText())!)
                            : null;
                        object? result = await tool.InvokeAsync(args, cancellationToken);
                        resultJson = JsonSerializer.Serialize(result);
                    }
                    else
                    {
                        resultJson = JsonSerializer.Serialize(new { error = $"Unknown tool: {toolCall.Name}" });
                    }

                    dtoMessages.Add(new ChatMessageDto
                    {
                        Role = "tool",
                        ToolCallId = toolCall.CallId,
                        Content = resultJson,
                    });
                }
            }
        }

        return new AIChatResponse(new ChatMessage(ChatRole.Assistant, "Tool processing limit reached."));
    }

    // [Note]
    // This implementation mimics streaming by yielding chunks from a completed response.
    // Ideally, this should use the /api/chat/stream endpoint for true SSE streaming.
    // However, the current server-side streaming endpoint does not support tool execution.
    // To maintain tool support for this sample, we use the non-streaming endpoint here.
    // A production implementation should implement tool support in the streaming endpoint
    // or use a separate client for streaming-only interactions.
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            if (message.Text is { Length: > 0 })
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ProxyChatClient) ? this : null;

    private IReadOnlyList<ToolDefinitionDto> BuildClientToolSchemas() =>
        clientTools.Tools.Select(t => new ToolDefinitionDto
        {
            Name = t.Name,
            Description = t.Description,
        }).ToList();

    private static List<ChatMessageDto> ConvertToDto(IEnumerable<ChatMessage> messages)
    {
        List<ChatMessageDto> dtos = [];
        foreach (var msg in messages)
        {
            var functionCalls = msg.Contents.OfType<FunctionCallContent>().ToList();
            if (functionCalls.Count > 0)
            {
                dtos.Add(new ChatMessageDto
                {
                    Role = msg.Role.Value,
                    Content = msg.Text,
                    ToolCalls = functionCalls.Select(fc => new ToolCallDto
                    {
                        CallId = fc.CallId,
                        Name = fc.Name,
                        Arguments = fc.Arguments is not null
                            ? JsonSerializer.SerializeToElement(fc.Arguments)
                            : null,
                    }).ToList(),
                });
                continue;
            }

            var functionResult = msg.Contents.OfType<FunctionResultContent>().FirstOrDefault();
            if (functionResult is not null)
            {
                dtos.Add(new ChatMessageDto
                {
                    Role = msg.Role.Value,
                    Content = functionResult.Result?.ToString(),
                    ToolCallId = functionResult.CallId,
                });
                continue;
            }

            dtos.Add(new ChatMessageDto
            {
                Role = msg.Role.Value,
                Content = msg.Text,
            });
        }
        return dtos;
    }

    private static AIChatResponse ConvertFromDto(Shared.Contracts.ChatResponse response)
    {
        List<ChatMessage> messages = response.Messages
            .Select(m => new ChatMessage(
                m.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    "tool" => ChatRole.Tool,
                    _ => ChatRole.User,
                },
                m.Content))
            .ToList();
        return new AIChatResponse(messages);
    }
}
