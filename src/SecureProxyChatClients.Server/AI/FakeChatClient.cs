using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.AI;

public sealed class FakeChatClient : IChatClient
{
    private static readonly ChatResponse DefaultResponse =
        new(new ChatMessage(ChatRole.Assistant, "This is a fake response."));

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
        ChatResponse response = Responses.Count > 0 ? Responses.Dequeue() : DefaultResponse;
        return Task.FromResult(response);
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
