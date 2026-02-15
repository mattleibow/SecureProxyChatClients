using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class GameToolRegistryTests
{
    private readonly GameToolRegistry _registry = new();

    [Fact]
    public void Contains_AllGameTools()
    {
        Assert.Equal(9, _registry.Tools.Count); // Increased to 9 with RecordCombatWin
    }

    [Theory]
    [InlineData("RollCheck")]
    [InlineData("MovePlayer")]
    [InlineData("GiveItem")]
    [InlineData("TakeItem")]
    [InlineData("ModifyHealth")]
    [InlineData("ModifyGold")]
    [InlineData("AwardExperience")]
    [InlineData("GenerateNpc")]
    [InlineData("RecordCombatWin")]
    public void GetTool_ReturnsRegisteredTool(string name)
    {
        var tool = _registry.GetTool(name);
        Assert.NotNull(tool);
    }

    [Fact]
    public void GetTool_ReturnsNull_ForUnknownTool()
    {
        var tool = _registry.GetTool("Teleport");
        Assert.Null(tool);
    }

    [Fact]
    public void ApplyToolResult_LocationResult_UpdatesPlayerLocation()
    {
        var state = new PlayerState();
        var result = new LocationResult("Dark Cave", "Dripping water echoes");

        GameToolRegistry.ApplyToolResult(result, state);

        Assert.Equal("Dark Cave", state.CurrentLocation);
    }

    [Fact]
    public void ApplyToolResult_GiveItem_AddsToInventory()
    {
        var state = new PlayerState();
        var result = new ItemResult("Torch", "Lights the way", "misc", "🔦", "common", Added: true);

        GameToolRegistry.ApplyToolResult(result, state);

        Assert.Single(state.Inventory);
        Assert.Equal("Torch", state.Inventory[0].Name);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_RemovesFromInventory()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Key" });
        var result = new ItemResult("Key", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(result, state);

        Assert.Empty(state.Inventory);
    }

    [Fact]
    public void ApplyToolResult_HealthResult_ClampsToMaxHealth()
    {
        var state = new PlayerState { Health = 90, MaxHealth = 100 };
        var heal = new HealthResult(50, "Potion");

        GameToolRegistry.ApplyToolResult(heal, state);

        Assert.Equal(100, state.Health);
    }

    [Fact]
    public void ApplyToolResult_HealthResult_ClampsToZero()
    {
        var state = new PlayerState { Health = 10 };
        var damage = new HealthResult(-50, "Dragon fire");

        GameToolRegistry.ApplyToolResult(damage, state);

        Assert.Equal(0, state.Health);
    }

    [Fact]
    public void ApplyToolResult_GoldResult_ClampsToZero()
    {
        var state = new PlayerState { Gold = 5 };
        var loss = new GoldResult(-20, "Robbed");

        GameToolRegistry.ApplyToolResult(loss, state);

        Assert.Equal(0, state.Gold);
    }

    [Fact]
    public void ApplyToolResult_ExperienceResult_TriggersLevelUp()
    {
        var state = new PlayerState { Level = 1, Experience = 90, Health = 100, MaxHealth = 100 };
        var xp = new ExperienceResult(20, "Quest complete");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(2, state.Level);
        Assert.Equal(10, state.Experience); // 110 - 100 (level 1 threshold) = 10 remainder
        Assert.Equal(110, state.MaxHealth);
        Assert.Equal(110, state.Health); // Healed on level up
    }

    [Fact]
    public void ApplyToolResult_CombatResult_AwardsAchievements()
    {
        var state = new PlayerState { Health = 100 };
        var win = new CombatResult("Goblin Scout");

        GameToolRegistry.ApplyToolResult(win, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_CombatResult_AwardsSurvivor()
    {
        var state = new PlayerState { Health = 4 }; // Low health
        var win = new CombatResult("Bandit Captain");

        GameToolRegistry.ApplyToolResult(win, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
        Assert.Contains("survivor", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_CombatResult_AwardsDragonSlayer()
    {
        var state = new PlayerState { Health = 50 };
        var win = new CombatResult("Ancient Dragon");

        GameToolRegistry.ApplyToolResult(win, state);

        Assert.Contains("dragon-slayer", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_NpcResult_StripsHiddenSecret()
    {
        var state = new PlayerState();
        var npc = new NpcResult("npc1", "Eva", "Scholar", "Wise woman", "Secretly a spy", "neutral");

        var clientResult = GameToolRegistry.ApplyToolResult(npc, state);

        // Should return anonymous object without HiddenSecret
        string json = System.Text.Json.JsonSerializer.Serialize(clientResult);
        Assert.DoesNotContain("Secretly a spy", json);
        Assert.Contains("Eva", json);
    }

    [Fact]
    public void ApplyToolResult_LocationResult_TracksVisitedLocations()
    {
        var state = new PlayerState();
        Assert.Single(state.VisitedLocations); // "The Crossroads" is default

        GameToolRegistry.ApplyToolResult(new LocationResult("Dark Forest", ""), state);
        GameToolRegistry.ApplyToolResult(new LocationResult("Ancient Temple", ""), state);

        Assert.Equal(3, state.VisitedLocations.Count);
        Assert.Contains("Dark Forest", state.VisitedLocations);
        Assert.Contains("Ancient Temple", state.VisitedLocations);
    }
}
