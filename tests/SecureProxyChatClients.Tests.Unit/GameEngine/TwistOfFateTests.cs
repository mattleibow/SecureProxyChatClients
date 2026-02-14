using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class TwistOfFateTests
{
    [Fact]
    public void GetRandomTwist_ReturnsValidTwist()
    {
        var twist = TwistOfFate.GetRandomTwist();

        Assert.False(string.IsNullOrWhiteSpace(twist.Title));
        Assert.False(string.IsNullOrWhiteSpace(twist.Prompt));
        Assert.False(string.IsNullOrWhiteSpace(twist.Emoji));
        Assert.False(string.IsNullOrWhiteSpace(twist.Category));
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("combat")]
    [InlineData("encounter")]
    [InlineData("discovery")]
    [InlineData("personal")]
    public void GetTwistByCategory_ReturnsCorrectCategory(string category)
    {
        var twist = TwistOfFate.GetTwistByCategory(category);

        Assert.Equal(category, twist.Category, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTwistByCategory_UnknownCategory_FallsBackToRandom()
    {
        var twist = TwistOfFate.GetTwistByCategory("nonexistent");

        Assert.NotNull(twist);
        Assert.False(string.IsNullOrWhiteSpace(twist.Title));
    }

    [Fact]
    public void GetRandomTwist_CanProduceDifferentResults()
    {
        // Over 20 tries, we should get at least 2 different twists
        var titles = Enumerable.Range(0, 20)
            .Select(_ => TwistOfFate.GetRandomTwist().Title)
            .Distinct()
            .ToList();

        Assert.True(titles.Count > 1, "Expected multiple different twists");
    }
}
