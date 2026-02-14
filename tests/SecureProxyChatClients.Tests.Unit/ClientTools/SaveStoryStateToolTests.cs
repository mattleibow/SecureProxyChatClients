using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class SaveStoryStateToolTests
{
    [Fact]
    public void SaveStoryState_CreatesNewScene()
    {
        var state = new StoryStateService();
        var tool = new SaveStoryStateTool(state);

        var result = tool.SaveStoryState("s1", "A dark cave");

        Assert.True(result.Success);
        Assert.Contains("created", result.Message);
        Assert.NotNull(state.GetScene("s1"));
    }

    [Fact]
    public void SaveStoryState_UpdatesExistingScene()
    {
        var state = new StoryStateService();
        state.AddScene(new Shared.Contracts.SceneResult
        {
            Id = "s1", Title = "s1", Description = "Old",
            Characters = [], Location = "L", Mood = "m",
        });
        var tool = new SaveStoryStateTool(state);

        var result = tool.SaveStoryState("s1", "Updated description");

        Assert.True(result.Success);
        Assert.Contains("saved", result.Message);
        Assert.Equal("Updated description", state.GetScene("s1")!.Description);
    }
}
