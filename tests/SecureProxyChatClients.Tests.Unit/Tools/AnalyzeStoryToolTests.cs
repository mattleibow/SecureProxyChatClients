using SecureProxyChatClients.Server.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Tools;

public class AnalyzeStoryToolTests
{
    [Fact]
    public void AnalyzeStory_ReturnsBaseThemes()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory("A story about a hero's journey.");

        Assert.Contains("identity", result.Themes);
        Assert.Contains("conflict", result.Themes);
    }

    [Fact]
    public void AnalyzeStory_DetectsMagicTheme()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory("The wizard used magic to defeat the dragon.");

        Assert.Contains("supernatural", result.Themes);
    }

    [Fact]
    public void AnalyzeStory_DetectsRomanceTheme()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory("Their love blossomed in the garden.");

        Assert.Contains("romance", result.Themes);
    }

    [Fact]
    public void AnalyzeStory_ShortContextProducesPlotHole()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory("Short.");

        Assert.NotEmpty(result.PlotHoles);
    }

    [Fact]
    public void AnalyzeStory_LongContextHasNoDefaultPlotHoles()
    {
        string longContext = new('x', 200);
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory(longContext);

        Assert.Empty(result.PlotHoles);
    }

    [Fact]
    public void AnalyzeStory_TensionLevelScalesWithLength()
    {
        AnalysisResult short_ = AnalyzeStoryTool.AnalyzeStory("Short");
        AnalysisResult long_ = AnalyzeStoryTool.AnalyzeStory(new string('x', 1000));

        Assert.True(long_.TensionLevel >= short_.TensionLevel);
    }

    [Fact]
    public void AnalyzeStory_TensionLevelClampedTo10()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory(new string('x', 5000));

        Assert.InRange(result.TensionLevel, 1, 10);
    }

    [Fact]
    public void AnalyzeStory_AlwaysReturnsSuggestions()
    {
        AnalysisResult result = AnalyzeStoryTool.AnalyzeStory("Any story context.");

        Assert.NotEmpty(result.Suggestions);
    }
}
