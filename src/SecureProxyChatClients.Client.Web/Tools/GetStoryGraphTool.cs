using System.ComponentModel;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Tools;

public class GetStoryGraphTool(StoryStateService storyState)
{
    [Description("Gets the current story graph from local storage")]
    public StoryGraphResult GetStoryGraph() => storyState.GetStoryGraph();
}
