using System.Reflection;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.Tools;

/// <summary>
/// Collects all server-side AIFunction tools for registration with IChatClient.
/// </summary>
public sealed class ServerToolRegistry
{
    private readonly IReadOnlyList<AIFunction> _tools =
    [
        AIFunctionFactory.Create(typeof(GenerateSceneTool).GetMethod(nameof(GenerateSceneTool.GenerateScene))!, target: null, options: null),
        AIFunctionFactory.Create(typeof(CreateCharacterTool).GetMethod(nameof(CreateCharacterTool.CreateCharacter))!, target: null, options: null),
        AIFunctionFactory.Create(typeof(AnalyzeStoryTool).GetMethod(nameof(AnalyzeStoryTool.AnalyzeStory))!, target: null, options: null),
        AIFunctionFactory.Create(typeof(SuggestTwistTool).GetMethod(nameof(SuggestTwistTool.SuggestTwist))!, target: null, options: null),
    ];

    public IReadOnlyList<AIFunction> Tools => _tools;

    /// <summary>
    /// Returns true if the given tool name is a registered server tool.
    /// </summary>
    public bool IsServerTool(string toolName) =>
        _tools.Any(t => t.Name == toolName);

    /// <summary>
    /// Finds a server tool by name, or returns null.
    /// </summary>
    public AIFunction? GetTool(string toolName) =>
        _tools.FirstOrDefault(t => t.Name == toolName);
}
