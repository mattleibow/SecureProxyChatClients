using System.ComponentModel;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Tools;

public class AnalyzeStoryTool
{
    [Description("Analyzes the current story state and returns themes, plot holes, and suggestions")]
    public static AnalysisResult AnalyzeStory(
        [Description("A summary of the current story context")] string storyContext)
    {
        // Deterministic analysis based on story context length as a simple heuristic
        int tensionLevel = Math.Clamp(storyContext.Length / 100, 1, 10);

        List<string> themes = ["identity", "conflict"];
        if (storyContext.Contains("magic", StringComparison.OrdinalIgnoreCase))
            themes.Add("supernatural");
        if (storyContext.Contains("love", StringComparison.OrdinalIgnoreCase))
            themes.Add("romance");

        List<string> plotHoles = [];
        if (storyContext.Length < 50)
            plotHoles.Add("Story context is too brief to identify a clear narrative arc");

        List<string> suggestions =
        [
            "Consider deepening character motivations",
            "Add environmental details to ground the reader",
        ];

        return new AnalysisResult
        {
            Themes = themes,
            PlotHoles = plotHoles,
            Suggestions = suggestions,
            TensionLevel = tensionLevel,
        };
    }
}
