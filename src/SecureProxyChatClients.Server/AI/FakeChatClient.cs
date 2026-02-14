using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

public sealed class FakeChatClient : IChatClient
{
    private static readonly ChatResponse DefaultResponse =
        new(new ChatMessage(ChatRole.Assistant, "This is a fake response."));

    private static readonly string[] CombatKeywords = ["attack", "fight", "slash", "strike", "hit", "stab", "swing", "shoot", "cast", "punch", "kick"];
    private static readonly string[] SearchKeywords = ["search", "look", "examine", "inspect", "investigate", "check"];
    private static readonly string[] MoveKeywords = ["go to", "head to", "walk to", "travel to", "move to", "enter"];

    public Queue<ChatResponse> Responses { get; } = new();
    public Queue<List<ChatResponseUpdate>> StreamingResponses { get; } = new();
    public List<IEnumerable<ChatMessage>> ReceivedMessages { get; } = [];
    public List<ChatOptions?> ReceivedOptions { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add(messages);
        ReceivedOptions.Add(options);

        if (Responses.Count > 0)
            return Task.FromResult(Responses.Dequeue());

        // When tools are available and user message matches action patterns, simulate tool calls
        if (options?.Tools is { Count: > 0 })
        {
            var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text?.ToLowerInvariant() ?? "";
            var toolCallResponse = GenerateToolCallResponse(lastUserMessage, messages);
            if (toolCallResponse is not null)
                return Task.FromResult(toolCallResponse);
        }

        return Task.FromResult(DefaultResponse);
    }

    private ChatResponse? GenerateToolCallResponse(string lastUserMessage, IEnumerable<ChatMessage> messages)
    {
        // If the last message was a tool result, generate the narrative response
        bool lastWasToolResult = messages.LastOrDefault()?.Role == ChatRole.Tool;
        if (lastWasToolResult)
            return null; // Return null to use default narrative response

        string callId = $"fake_{Guid.NewGuid():N}"[..16];

        if (CombatKeywords.Any(k => lastUserMessage.Contains(k)))
        {
            // Simulate a RollCheck tool call for combat
            var args = new Dictionary<string, object?>
            {
                ["stat"] = "dexterity",
                ["difficulty"] = 10,
                ["action"] = "Attack with weapon",
            };
            var functionCall = new FunctionCallContent(callId, "RollCheck", args);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [functionCall]));
        }

        if (SearchKeywords.Any(k => lastUserMessage.Contains(k)))
        {
            var args = new Dictionary<string, object?>
            {
                ["stat"] = "wisdom",
                ["difficulty"] = 12,
                ["action"] = "Search the area carefully",
            };
            var functionCall = new FunctionCallContent(callId, "RollCheck", args);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [functionCall]));
        }

        return null;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add(messages);
        ReceivedOptions.Add(options);

        if (StreamingResponses.Count > 0)
        {
            foreach (ChatResponseUpdate update in StreamingResponses.Dequeue())
            {
                yield return update;
                await Task.Delay(10, cancellationToken);
            }
        }
        else
        {
            // Default: yield text from the default response word by word
            ChatResponse response = Responses.Count > 0 ? Responses.Dequeue() : DefaultResponse;
            string text = response.Messages[0].Text ?? "Fake response";
            foreach (string word in text.Split(' '))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Helper to enqueue a response containing FunctionCallContent for tool call simulation.
    /// </summary>
    public void EnqueueToolCallResponse(string callId, string toolName, IDictionary<string, object?>? arguments = null)
    {
        var functionCallContent = new FunctionCallContent(callId, toolName, arguments);
        var message = new ChatMessage(ChatRole.Assistant, [functionCallContent]);
        Responses.Enqueue(new ChatResponse(message));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(FakeChatClient) ? this : null;

    public void Dispose() { }
}
