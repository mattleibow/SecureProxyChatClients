using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Client.Web.Agents;

/// <summary>
/// Group chat orchestration: round-robin discussion among LoreAgents.
/// </summary>
public sealed class WritersRoom(IReadOnlyList<LoreAgent> agents)
{
    public IReadOnlyList<LoreAgent> Agents => agents;

    public async IAsyncEnumerable<AgentMessage> RunDiscussionAsync(
        string userPitch,
        int maxRounds = 3,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var conversation = new List<ChatMessage>
        {
            new(ChatRole.User, userPitch),
        };

        for (int round = 0; round < maxRounds; round++)
        {
            foreach (var agent in agents)
            {
                ct.ThrowIfCancellationRequested();

                string response = await agent.RespondAsync(conversation, ct);

                if (string.IsNullOrWhiteSpace(response))
                    continue;

                // Add the agent's response to the shared conversation so subsequent agents see it
                conversation.Add(new ChatMessage(ChatRole.Assistant, $"[{agent.Name}]: {response}"));

                bool isFinal = round == maxRounds - 1 && agent == agents[^1];
                yield return new AgentMessage(agent.Name, agent.Emoji, response, isFinal);
            }
        }
    }
}
