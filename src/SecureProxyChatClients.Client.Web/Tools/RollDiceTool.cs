using System.ComponentModel;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Tools;

public class RollDiceTool
{
    [Description("Rolls dice for game mechanics")]
    public static DiceResult RollDice(
        [Description("Number of dice to roll")] int count,
        [Description("Number of sides per die")] int sides)
    {
        var random = new Random();
        var rolls = Enumerable.Range(0, count).Select(_ => random.Next(1, sides + 1)).ToList();
        return new DiceResult { Rolls = rolls, Total = rolls.Sum() };
    }
}
