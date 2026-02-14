using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Client.Web.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class GetWorldRulesToolTests
{
    [Fact]
    public void GetWorldRules_ReturnsCurrentRules()
    {
        var state = new StoryStateService();
        state.AddWorldRule(new WorldRule { Name = "Magic", Description = "Elemental only" });
        state.AddWorldRule(new WorldRule { Name = "Travel", Description = "No teleportation" });

        var tool = new GetWorldRulesTool(state);
        WorldRulesResult result = tool.GetWorldRules();

        Assert.Equal(2, result.Rules.Count);
    }

    [Fact]
    public void GetWorldRules_ReturnsEmptyWhenNoRules()
    {
        var state = new StoryStateService();
        var tool = new GetWorldRulesTool(state);

        WorldRulesResult result = tool.GetWorldRules();

        Assert.Empty(result.Rules);
    }
}
