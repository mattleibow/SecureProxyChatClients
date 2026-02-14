using System.ComponentModel;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Tools;

public class GetWorldRulesTool(StoryStateService storyState)
{
    [Description("Gets the world-building rules for the current story")]
    public WorldRulesResult GetWorldRules() => storyState.GetWorldRules();
}
