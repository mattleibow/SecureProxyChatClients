using SecureProxyChatClients.Server.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Tools;

public class GenerateSceneToolTests
{
    [Fact]
    public void GenerateScene_ReturnsSceneWithCorrectMoodAndGenre()
    {
        SceneResult result = GenerateSceneTool.GenerateScene("A dark forest", "fantasy", "mysterious");

        Assert.Equal("mysterious", result.Mood);
        Assert.Contains("fantasy", result.Description);
        Assert.Contains("dark forest", result.Description);
        Assert.StartsWith("scene-", result.Id);
    }

    [Fact]
    public void GenerateScene_TitleContainsMoodAndGenre()
    {
        SceneResult result = GenerateSceneTool.GenerateScene("test", "horror", "tense");

        Assert.Contains("Tense", result.Title);
        Assert.Contains("Horror", result.Title);
    }

    [Fact]
    public void GenerateScene_HasAtLeastOneCharacter()
    {
        SceneResult result = GenerateSceneTool.GenerateScene("opening", "sci-fi", "epic");

        Assert.NotEmpty(result.Characters);
    }

    [Theory]
    [InlineData("fantasy", "peaceful")]
    [InlineData("sci-fi", "tense")]
    [InlineData("mystery", "mysterious")]
    [InlineData("horror", "epic")]
    public void GenerateScene_WorksWithAllGenreMoodCombinations(string genre, string mood)
    {
        SceneResult result = GenerateSceneTool.GenerateScene("test prompt", genre, mood);

        Assert.NotNull(result.Id);
        Assert.NotNull(result.Title);
        Assert.NotNull(result.Description);
        Assert.NotNull(result.Location);
        Assert.Equal(mood, result.Mood);
    }
}
