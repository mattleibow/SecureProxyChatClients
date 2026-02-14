using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class SearchStoryToolTests
{
    [Fact]
    public void SearchStory_ReturnsMatchingResults()
    {
        var state = new StoryStateService();
        state.AddScene(new Shared.Contracts.SceneResult
        {
            Id = "s1", Title = "Dragon's Lair", Description = "A cave full of treasure",
            Characters = [], Location = "Mountain", Mood = "tense",
        });

        var tool = new SearchStoryTool(state);
        var result = tool.SearchStory("dragon");

        Assert.Single(result.Matches);
    }

    [Fact]
    public void SearchStory_ReturnsEmptyForNoMatch()
    {
        var state = new StoryStateService();
        var tool = new SearchStoryTool(state);

        var result = tool.SearchStory("nothing");

        Assert.Empty(result.Matches);
    }
}
