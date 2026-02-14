using SecureProxyChatClients.Client.Web.Tools;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class RollDiceToolTests
{
    [Fact]
    public void RollDice_ReturnsCorrectNumberOfRolls()
    {
        var result = RollDiceTool.RollDice(3, 6);

        Assert.Equal(3, result.Rolls.Count);
    }

    [Fact]
    public void RollDice_TotalMatchesSumOfRolls()
    {
        var result = RollDiceTool.RollDice(4, 8);

        Assert.Equal(result.Rolls.Sum(), result.Total);
    }

    [Fact]
    public void RollDice_ValuesWithinRange()
    {
        var result = RollDiceTool.RollDice(100, 6);

        Assert.All(result.Rolls, r =>
        {
            Assert.InRange(r, 1, 6);
        });
    }

    [Fact]
    public void RollDice_SingleDie()
    {
        var result = RollDiceTool.RollDice(1, 20);

        Assert.Single(result.Rolls);
        Assert.InRange(result.Rolls[0], 1, 20);
        Assert.Equal(result.Rolls[0], result.Total);
    }
}
