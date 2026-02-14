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

    [Fact]
    public void GameEvent_DiceCheckResult_RoundtripsViaSerialization()
    {
        var diceResult = GameTools.RollCheck("dexterity", 12, "Dodge attack");
        var gameEvent = new SecureProxyChatClients.Shared.Contracts.GameEvent
        {
            Type = "RollCheck",
            Data = System.Text.Json.JsonSerializer.SerializeToElement(diceResult),
        };

        // Serialize and deserialize (simulating SSE roundtrip)
        string json = System.Text.Json.JsonSerializer.Serialize(gameEvent);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SecureProxyChatClients.Shared.Contracts.GameEvent>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        Assert.Equal("RollCheck", deserialized.Type);
        Assert.NotNull(deserialized.Data);

        // Verify the Data properties are accessible with PascalCase
        var data = deserialized.Data.Value;
        Assert.Equal(diceResult.Roll, data.GetProperty("Roll").GetInt32());
        Assert.Equal(diceResult.Total, data.GetProperty("Total").GetInt32());
        Assert.Equal(diceResult.Difficulty, data.GetProperty("Difficulty").GetInt32());
        Assert.Equal(diceResult.Success, data.GetProperty("Success").GetBoolean());
    }

    [Fact]
    public void GameEvent_HealthResult_RoundtripsViaSerialization()
    {
        var healthResult = new HealthResult(-15, "Dragon fire");
        var gameEvent = new SecureProxyChatClients.Shared.Contracts.GameEvent
        {
            Type = "ModifyHealth",
            Data = System.Text.Json.JsonSerializer.SerializeToElement(healthResult),
        };

        string json = System.Text.Json.JsonSerializer.Serialize(gameEvent);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SecureProxyChatClients.Shared.Contracts.GameEvent>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        var data = deserialized.Data!.Value;
        Assert.Equal(-15, data.GetProperty("Amount").GetInt32());
        Assert.Equal("Dragon fire", data.GetProperty("Source").GetString());
    }

    // --- Input validation tests for game tools ---

    [Fact]
    public void RollCheck_ClampsDifficultyToValidRange()
    {
        var low = GameTools.RollCheck("strength", -5, "test");
        var high = GameTools.RollCheck("strength", 100, "test");
        Assert.Equal(1, low.Difficulty);
        Assert.Equal(30, high.Difficulty);
    }

    [Fact]
    public void RollCheck_HandlesNullStat()
    {
        var result = GameTools.RollCheck(null!, 10, "test");
        Assert.Equal("strength", result.Stat);
        Assert.Equal(2, result.Modifier);
    }

    [Fact]
    public void RollCheck_TruncatesLongAction()
    {
        string longAction = new('x', 1000);
        var result = GameTools.RollCheck("strength", 10, longAction);
        Assert.Equal(500, result.Action.Length);
    }

    [Fact]
    public void GiveItem_NormalizesInvalidType()
    {
        var result = GameTools.GiveItem("Thing", "desc", "invalid_type", "📦");
        Assert.Equal("misc", result.Type);
    }

    [Fact]
    public void GiveItem_NormalizesInvalidRarity()
    {
        var result = GameTools.GiveItem("Thing", "desc", "weapon", "⚔️", "mythic");
        Assert.Equal("common", result.Rarity);
    }

    [Fact]
    public void ModifyHealth_ClampsExtremeValues()
    {
        var tooLow = GameTools.ModifyHealth(-9999, "nuke");
        var tooHigh = GameTools.ModifyHealth(9999, "mega heal");
        Assert.Equal(-200, tooLow.Amount);
        Assert.Equal(200, tooHigh.Amount);
    }

    [Fact]
    public void ModifyGold_ClampsExtremeValues()
    {
        var tooLow = GameTools.ModifyGold(-9999, "scam");
        var tooHigh = GameTools.ModifyGold(99999, "infinite money");
        Assert.Equal(-1000, tooLow.Amount);
        Assert.Equal(1000, tooHigh.Amount);
    }

    [Fact]
    public void AwardExperience_ClampsNegativeToZero()
    {
        var result = GameTools.AwardExperience(-100, "negative xp");
        Assert.Equal(0, result.Amount);
    }

    [Fact]
    public void AwardExperience_ClampsAboveMax()
    {
        var result = GameTools.AwardExperience(99999, "too much xp");
        Assert.Equal(5000, result.Amount);
    }

    [Fact]
    public void GenerateNpc_NormalizesInvalidAttitude()
    {
        var result = GameTools.GenerateNpc("Bob", "Guard", "Big guy", "None", "angry");
        Assert.Equal("neutral", result.Attitude);
    }

    [Fact]
    public void GiveItem_TruncatesLongEmoji()
    {
        var result = GameTools.GiveItem("Sword", "desc", "weapon", string.Concat(Enumerable.Repeat("x", 20)));
        Assert.True(result.Emoji.Length <= 10);
    }
}
