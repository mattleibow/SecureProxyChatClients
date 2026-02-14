using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class GetStoryGraphToolTests
{
    [Fact]
    public void GetStoryGraph_ReturnsCurrentState()
    {
        var state = new StoryStateService();
        state.AddScene(new SceneResult
        {
            Id = "s1", Title = "Opening", Description = "Start",
            Characters = [], Location = "Town", Mood = "peaceful",
        });

        var tool = new GetStoryGraphTool(state);
        StoryGraphResult result = tool.GetStoryGraph();

        Assert.Single(result.Scenes);
        Assert.Equal("s1", result.Scenes[0].Id);
    }
}
