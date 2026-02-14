namespace SecureProxyChatClients.Client.Web.Agents;

/// <summary>
/// A message produced by an agent during a WritersRoom discussion.
/// </summary>
public sealed record AgentMessage(string AgentName, string AgentEmoji, string Content, bool IsFinal);
