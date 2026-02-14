using SecureProxyChatClients.Server.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Tools;

public class SuggestTwistToolTests
{
    [Fact]
    public void SuggestTwist_LowTension_ReturnsMysteriousStranger()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("A calm beginning.", 2);

        Assert.Contains("mysterious stranger", result.Description);
    }

    [Fact]
    public void SuggestTwist_MidTension_ReturnsAllyBetrayal()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("Rising action.", 5);

        Assert.Contains("ally", result.Description);
    }

    [Fact]
    public void SuggestTwist_HighTension_ReturnsUnmasking()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("Climax approaching.", 8);

        Assert.Contains("antagonist", result.Description);
    }

    [Fact]
    public void SuggestTwist_MaxTension_ReturnsWorldChange()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("Peak climax.", 10);

        Assert.Contains("illusion", result.Description);
    }

    [Fact]
    public void SuggestTwist_ImpactLevelIncreasesWithTension()
    {
        TwistResult low = SuggestTwistTool.SuggestTwist("context", 1);
        TwistResult high = SuggestTwistTool.SuggestTwist("context", 8);

        Assert.True(high.ImpactLevel >= low.ImpactLevel);
    }

    [Fact]
    public void SuggestTwist_ImpactLevelClampedTo10()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("context", 10);

        Assert.InRange(result.ImpactLevel, 1, 10);
    }

    [Fact]
    public void SuggestTwist_AlwaysIncludesProtagonist()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("context", 3);

        Assert.Contains("protagonist", result.AffectedCharacters);
    }

    [Fact]
    public void SuggestTwist_HighTension_IncludesAntagonist()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("context", 7);

        Assert.Contains("antagonist", result.AffectedCharacters);
    }

    [Fact]
    public void SuggestTwist_VeryHighTension_IncludesMentor()
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("context", 9);

        Assert.Contains("mentor", result.AffectedCharacters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(15)]
    public void SuggestTwist_ClampsTensionToValidRange(int tension)
    {
        TwistResult result = SuggestTwistTool.SuggestTwist("context", tension);

        Assert.InRange(result.ImpactLevel, 1, 10);
        Assert.NotEmpty(result.Description);
    }
}
