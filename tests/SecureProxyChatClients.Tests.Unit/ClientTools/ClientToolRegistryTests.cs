using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class ClientToolRegistryTests
{
    private readonly ClientToolRegistry _registry;

    public ClientToolRegistryTests()
    {
        var state = new StoryStateService();
        _registry = new ClientToolRegistry(
            new GetStoryGraphTool(state),
            new SearchStoryTool(state),
            new SaveStoryStateTool(state),
            new GetWorldRulesTool(state));
    }

    [Fact]
    public void Tools_ContainsFiveTools()
    {
        Assert.Equal(5, _registry.Tools.Count);
    }

    [Theory]
    [InlineData("GetStoryGraph")]
    [InlineData("SearchStory")]
    [InlineData("SaveStoryState")]
    [InlineData("RollDice")]
    [InlineData("GetWorldRules")]
    public void IsClientTool_ReturnsTrueForRegisteredTools(string toolName)
    {
        Assert.True(_registry.IsClientTool(toolName));
    }

    [Theory]
    [InlineData("GenerateScene")]
    [InlineData("UnknownTool")]
    public void IsClientTool_ReturnsFalseForUnregisteredTools(string toolName)
    {
        Assert.False(_registry.IsClientTool(toolName));
    }

    [Fact]
    public void GetTool_ReturnsNullForUnknownTool()
    {
        Assert.Null(_registry.GetTool("NonExistent"));
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
