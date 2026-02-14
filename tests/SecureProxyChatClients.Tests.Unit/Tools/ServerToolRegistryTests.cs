using SecureProxyChatClients.Server.Tools;

namespace SecureProxyChatClients.Tests.Unit.Tools;

public class ServerToolRegistryTests
{
    private readonly ServerToolRegistry _registry = new();

    [Fact]
    public void Tools_ContainsFourTools()
    {
        Assert.Equal(4, _registry.Tools.Count);
    }

    [Theory]
    [InlineData("GenerateScene")]
    [InlineData("CreateCharacter")]
    [InlineData("AnalyzeStory")]
    [InlineData("SuggestTwist")]
    public void IsServerTool_ReturnsTrueForRegisteredTools(string toolName)
    {
        Assert.True(_registry.IsServerTool(toolName));
    }

    [Theory]
    [InlineData("GetStoryGraph")]
    [InlineData("RollDice")]
    [InlineData("UnknownTool")]
    public void IsServerTool_ReturnsFalseForUnregisteredTools(string toolName)
    {
        Assert.False(_registry.IsServerTool(toolName));
    }

    [Theory]
    [InlineData("GenerateScene")]
    [InlineData("CreateCharacter")]
    [InlineData("AnalyzeStory")]
    [InlineData("SuggestTwist")]
    public void GetTool_ReturnsToolForRegisteredNames(string toolName)
    {
        var tool = _registry.GetTool(toolName);

        Assert.NotNull(tool);
        Assert.Equal(toolName, tool.Name);
    }

    [Fact]
    public void GetTool_ReturnsNullForUnknownTool()
    {
        var tool = _registry.GetTool("NonExistent");

        Assert.Null(tool);
    }

    [Fact]
    public void Tools_AllHaveDescriptions()
    {
        foreach (var tool in _registry.Tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"Tool '{tool.Name}' should have a description");
        }
    }
}
