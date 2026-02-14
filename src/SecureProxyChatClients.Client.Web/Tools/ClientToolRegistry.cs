using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Client.Web.Tools;

/// <summary>
/// Collects all client-side AIFunction tools for use with ProxyChatClient.
/// </summary>
public sealed class ClientToolRegistry
{
    private readonly IReadOnlyList<AIFunction> _tools;

    public ClientToolRegistry(
        GetStoryGraphTool getStoryGraph,
        SearchStoryTool searchStory,
        SaveStoryStateTool saveStoryState,
        GetWorldRulesTool getWorldRules)
    {
        _tools =
        [
            AIFunctionFactory.Create(getStoryGraph.GetStoryGraph),
            AIFunctionFactory.Create(searchStory.SearchStory),
            AIFunctionFactory.Create(saveStoryState.SaveStoryState),
            AIFunctionFactory.Create(RollDiceTool.RollDice),
            AIFunctionFactory.Create(getWorldRules.GetWorldRules),
        ];
    }

    public IReadOnlyList<AIFunction> Tools => _tools;

    public bool IsClientTool(string toolName) =>
        _tools.Any(t => t.Name == toolName);

    public AIFunction? GetTool(string toolName) =>
        _tools.FirstOrDefault(t => t.Name == toolName);
}
