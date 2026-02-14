using System.ComponentModel;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Tools;

public class SearchStoryTool(StoryStateService storyState)
{
    [Description("Searches the local story data for matching content")]
    public SearchResult SearchStory(
        [Description("The text query to search for")] string query) =>
        storyState.Search(query);
}
