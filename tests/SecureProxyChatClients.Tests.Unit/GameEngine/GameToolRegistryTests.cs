using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class GameToolRegistryTests
{
    private readonly GameToolRegistry _registry = new();

    [Fact]
    public void Contains_AllGameTools()
    {
        Assert.Equal(8, _registry.Tools.Count);
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
        var result = new ItemResult("Torch", "Lights the way", "misc", "🔦", Added: true);

        GameToolRegistry.ApplyToolResult(result, state);

        Assert.Single(state.Inventory);
        Assert.Equal("Torch", state.Inventory[0].Name);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_RemovesFromInventory()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Key" });
        var result = new ItemResult("Key", "", "", "", Added: false);

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
        Assert.Equal(110, state.MaxHealth);
        Assert.Equal(110, state.Health); // Healed on level up
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
}
