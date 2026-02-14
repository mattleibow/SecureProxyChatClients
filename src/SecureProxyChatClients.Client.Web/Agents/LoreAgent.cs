using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Client.Web.Agents;

/// <summary>
/// A lightweight agent that wraps an IChatClient with a persona (system prompt).
/// Each agent uses ProxyChatClient under the hood, routing through the secure server proxy.
/// </summary>
public sealed class LoreAgent(string name, string emoji, string systemPrompt, IChatClient chatClient)
{
    public string Name { get; } = name;
    public string Emoji { get; } = emoji;

    public async Task<string> RespondAsync(IReadOnlyList<ChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>(conversationHistory.Count + 1)
        {
            new(ChatRole.System, systemPrompt),
        };
        messages.AddRange(conversationHistory);

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);

        return response.Messages
            .Where(m => m.Role == ChatRole.Assistant && m.Text is { Length: > 0 })
            .Select(m => m.Text!)
            .LastOrDefault() ?? string.Empty;
    }
}
