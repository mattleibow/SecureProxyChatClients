using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class GameToolsTests
{
    [Fact]
    public void RollCheck_ReturnsResultWithinRange()
    {
        var result = GameTools.RollCheck("strength", 10, "Lift the boulder");

        Assert.InRange(result.Roll, 1, 20);
        Assert.Equal("strength", result.Stat);
        Assert.Equal("Lift the boulder", result.Action);
        Assert.Equal(10, result.Difficulty);
    }

    [Fact]
    public void RollCheck_StrengthHasModifier()
    {
        var result = GameTools.RollCheck("strength", 1, "test");
        Assert.Equal(2, result.Modifier);
        Assert.Equal(result.Roll + result.Modifier, result.Total);
    }

    [Fact]
    public void RollCheck_WisdomHasCorrectModifier()
    {
        var result = GameTools.RollCheck("wisdom", 1, "test");
        Assert.Equal(1, result.Modifier);
    }

    [Fact]
    public void RollCheck_UnknownStatHasZeroModifier()
    {
        var result = GameTools.RollCheck("cooking", 10, "test");
        Assert.Equal(0, result.Modifier);
    }

    [Fact]
    public void MovePlayer_ReturnsLocationResult()
    {
        var result = GameTools.MovePlayer("Dark Forest", "Twisted trees loom overhead");

        Assert.Equal("Dark Forest", result.Location);
        Assert.Equal("Twisted trees loom overhead", result.Description);
    }

    [Fact]
    public void GiveItem_ReturnsItemResult()
    {
        var result = GameTools.GiveItem("Sword", "Sharp blade", "weapon", "⚔️");

        Assert.Equal("Sword", result.Name);
        Assert.Equal("weapon", result.Type);
        Assert.True(result.Added);
    }

    [Fact]
    public void TakeItem_ReturnsRemovedResult()
    {
        var result = GameTools.TakeItem("Key");

        Assert.Equal("Key", result.Name);
        Assert.False(result.Added);
    }

    [Fact]
    public void ModifyHealth_ReturnsHealthResult()
    {
        var result = GameTools.ModifyHealth(-15, "Goblin attack");

        Assert.Equal(-15, result.Amount);
        Assert.Equal("Goblin attack", result.Source);
    }

    [Fact]
    public void ModifyGold_ReturnsGoldResult()
    {
        var result = GameTools.ModifyGold(50, "Treasure chest");

        Assert.Equal(50, result.Amount);
        Assert.Equal("Treasure chest", result.Reason);
    }

    [Fact]
    public void AwardExperience_ReturnsExperienceResult()
    {
        var result = GameTools.AwardExperience(25, "Defeated goblin");

        Assert.Equal(25, result.Amount);
        Assert.Equal("Defeated goblin", result.Reason);
    }

    [Fact]
    public void GenerateNpc_ReturnsNpcWithHiddenSecret()
    {
        var result = GameTools.GenerateNpc("Bob", "Innkeeper", "Jolly man", "Is a vampire", "friendly");

        Assert.Equal("Bob", result.Name);
        Assert.Equal("Is a vampire", result.HiddenSecret);
        Assert.Equal("friendly", result.Attitude);
        Assert.NotNull(result.Id);
        Assert.Equal(8, result.Id.Length);
    }
}
