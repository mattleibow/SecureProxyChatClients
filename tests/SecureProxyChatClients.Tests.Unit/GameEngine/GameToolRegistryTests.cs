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

    // ── Combat Edge Cases ───────────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_HealthDamage_KillsPlayerAtZero()
    {
        var state = new PlayerState { Health = 10 };
        var damage = new HealthResult(-10, "Fatal blow");

        GameToolRegistry.ApplyToolResult(damage, state);

        Assert.Equal(0, state.Health);
    }

    [Fact]
    public void ApplyToolResult_HealthDamage_ExceedsHealth_ClampsToZero()
    {
        var state = new PlayerState { Health = 30 };
        var damage = new HealthResult(-100, "Massive attack");

        GameToolRegistry.ApplyToolResult(damage, state);

        Assert.Equal(0, state.Health);
    }

    [Fact]
    public void ApplyToolResult_HealAtFullHP_StaysAtMax()
    {
        var state = new PlayerState { Health = 100, MaxHealth = 100 };
        var heal = new HealthResult(50, "Potion");

        GameToolRegistry.ApplyToolResult(heal, state);

        Assert.Equal(100, state.Health);
    }

    [Fact]
    public void ApplyToolResult_HealFromZero()
    {
        var state = new PlayerState { Health = 0, MaxHealth = 100 };
        var heal = new HealthResult(25, "Revive potion");

        GameToolRegistry.ApplyToolResult(heal, state);

        Assert.Equal(25, state.Health);
    }

    [Fact]
    public void ApplyToolResult_HealthDamage_ZeroDamage_NoChange()
    {
        var state = new PlayerState { Health = 50 };
        var damage = new HealthResult(0, "Blocked");

        GameToolRegistry.ApplyToolResult(damage, state);

        Assert.Equal(50, state.Health);
    }

    // ── Gold Edge Cases ─────────────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_GoldSpend_MoreThanAvailable_ClampsToZero()
    {
        var state = new PlayerState { Gold = 15 };
        var spend = new GoldResult(-50, "Expensive purchase");

        GameToolRegistry.ApplyToolResult(spend, state);

        Assert.Equal(0, state.Gold);
    }

    [Fact]
    public void ApplyToolResult_GoldSpend_ExactAmount_ReachesZero()
    {
        var state = new PlayerState { Gold = 25 };
        var spend = new GoldResult(-25, "Exact price");

        GameToolRegistry.ApplyToolResult(spend, state);

        Assert.Equal(0, state.Gold);
    }

    [Fact]
    public void ApplyToolResult_GoldGain_FromZero()
    {
        var state = new PlayerState { Gold = 0 };
        var gain = new GoldResult(100, "Treasure");

        GameToolRegistry.ApplyToolResult(gain, state);

        Assert.Equal(100, state.Gold);
    }

    [Fact]
    public void ApplyToolResult_GoldSpend_AlreadyAtZero_StaysZero()
    {
        var state = new PlayerState { Gold = 0 };
        var spend = new GoldResult(-10, "Can't afford");

        GameToolRegistry.ApplyToolResult(spend, state);

        Assert.Equal(0, state.Gold);
    }

    [Fact]
    public void ApplyToolResult_GoldGain_LargeAmount()
    {
        var state = new PlayerState { Gold = 50 };
        var gain = new GoldResult(1000, "Dragon hoard");

        GameToolRegistry.ApplyToolResult(gain, state);

        Assert.Equal(1050, state.Gold);
    }

    // ── Inventory Edge Cases ────────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_TakeItem_NotInInventory_NoChange()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Sword" });
        var take = new ItemResult("Shield", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Single(state.Inventory);
        Assert.Equal("Sword", state.Inventory[0].Name);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_EmptyInventory_NoError()
    {
        var state = new PlayerState();
        var take = new ItemResult("Anything", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Empty(state.Inventory);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_CaseInsensitiveMatch()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Magic Sword" });
        var take = new ItemResult("magic sword", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Empty(state.Inventory);
    }

    [Fact]
    public void ApplyToolResult_GiveItem_DuplicateName_BothKept()
    {
        var state = new PlayerState();
        GameToolRegistry.ApplyToolResult(
            new ItemResult("Potion", "Heals", "potion", "🧪", "common", Added: true), state);
        GameToolRegistry.ApplyToolResult(
            new ItemResult("Potion", "Heals more", "potion", "🧪", "uncommon", Added: true), state);

        Assert.Equal(2, state.Inventory.Count);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_RemovesOnlyFirstMatch()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Potion", Description = "First" });
        state.Inventory.Add(new InventoryItem { Name = "Potion", Description = "Second" });
        var take = new ItemResult("Potion", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Single(state.Inventory);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_StackableDecrementsQuantity()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Healing Potion", Quantity = 3 });
        var take = new ItemResult("Healing Potion", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Single(state.Inventory);
        Assert.Equal(2, state.Inventory[0].Quantity);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_LastOfStack_RemovesItem()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Arrow", Quantity = 1 });
        var take = new ItemResult("Arrow", "", "", "", "common", Added: false);

        GameToolRegistry.ApplyToolResult(take, state);

        Assert.Empty(state.Inventory);
    }

    [Fact]
    public void ApplyToolResult_TakeItem_MultipleUses_DrainStack()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Bomb", Quantity = 3 });

        GameToolRegistry.ApplyToolResult(
            new ItemResult("Bomb", "", "", "", "common", Added: false), state);
        Assert.Equal(2, state.Inventory[0].Quantity);

        GameToolRegistry.ApplyToolResult(
            new ItemResult("Bomb", "", "", "", "common", Added: false), state);
        Assert.Equal(1, state.Inventory[0].Quantity);

        GameToolRegistry.ApplyToolResult(
            new ItemResult("Bomb", "", "", "", "common", Added: false), state);
        Assert.Empty(state.Inventory);
    }

    // ── XP / Level Edge Cases ───────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_ExactXPThreshold_LevelsUp()
    {
        // Level 1 threshold = 100 XP. Exactly 100 should trigger level up.
        var state = new PlayerState { Level = 1, Experience = 0, Health = 100, MaxHealth = 100 };
        var xp = new ExperienceResult(100, "Quest");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(2, state.Level);
        Assert.Equal(0, state.Experience); // No remainder
        Assert.Equal(110, state.MaxHealth);
        Assert.Equal(110, state.Health);
    }

    [Fact]
    public void ApplyToolResult_XPJustBelowThreshold_NoLevelUp()
    {
        var state = new PlayerState { Level = 1, Experience = 0 };
        var xp = new ExperienceResult(99, "Almost");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(1, state.Level);
        Assert.Equal(99, state.Experience);
    }

    [Fact]
    public void ApplyToolResult_MultiLevelUp_350XPAtLevel1()
    {
        // 350 XP at level 1: 350≥100 → level 2 (250 left), 250≥200 → level 3 (50 left), 50<300 → stop
        var state = new PlayerState { Level = 1, Experience = 0, Health = 100, MaxHealth = 100 };
        var xp = new ExperienceResult(350, "Epic quest");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(3, state.Level);
        Assert.Equal(50, state.Experience);
        Assert.Equal(120, state.MaxHealth); // 100 + 10 + 10
        Assert.Equal(120, state.Health); // Full heal on each level up
    }

    [Fact]
    public void ApplyToolResult_ZeroXP_NoChange()
    {
        var state = new PlayerState { Level = 1, Experience = 50 };
        var xp = new ExperienceResult(0, "Nothing");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(1, state.Level);
        Assert.Equal(50, state.Experience);
    }

    [Fact]
    public void ApplyToolResult_LevelUpHealsPlayer()
    {
        var state = new PlayerState { Level = 1, Experience = 0, Health = 10, MaxHealth = 100 };
        var xp = new ExperienceResult(100, "Quest");

        GameToolRegistry.ApplyToolResult(xp, state);

        Assert.Equal(2, state.Level);
        Assert.Equal(110, state.Health); // Full heal to new max
        Assert.Equal(110, state.MaxHealth);
    }

    [Fact]
    public void ApplyToolResult_AccumulateXPAcrossMultipleAwards()
    {
        var state = new PlayerState { Level = 1, Experience = 0, Health = 100, MaxHealth = 100 };

        GameToolRegistry.ApplyToolResult(new ExperienceResult(40, "Small quest"), state);
        Assert.Equal(1, state.Level);
        Assert.Equal(40, state.Experience);

        GameToolRegistry.ApplyToolResult(new ExperienceResult(40, "Another quest"), state);
        Assert.Equal(1, state.Level);
        Assert.Equal(80, state.Experience);

        GameToolRegistry.ApplyToolResult(new ExperienceResult(40, "Final push"), state);
        Assert.Equal(2, state.Level);
        Assert.Equal(20, state.Experience); // 120 - 100 = 20
    }

    // ── Dice / Streak Tests ─────────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_DiceSuccess_IncrementsStreak()
    {
        var state = new PlayerState { SuccessStreak = 0, MaxStreak = 0 };
        var dice = new DiceCheckResult(15, 2, 17, 10, Success: true,
            CriticalSuccess: false, CriticalFailure: false, "test", "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Equal(1, state.SuccessStreak);
        Assert.Equal(1, state.MaxStreak);
    }

    [Fact]
    public void ApplyToolResult_DiceFailure_ResetsStreak()
    {
        var state = new PlayerState { SuccessStreak = 5, MaxStreak = 5 };
        var dice = new DiceCheckResult(3, 0, 3, 10, Success: false,
            CriticalSuccess: false, CriticalFailure: false, "test", "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Equal(0, state.SuccessStreak);
        Assert.Equal(5, state.MaxStreak); // Max preserved
    }

    [Fact]
    public void ApplyToolResult_DiceSuccess_MaxStreakTracked()
    {
        var state = new PlayerState { SuccessStreak = 0, MaxStreak = 3 };
        var success = new DiceCheckResult(15, 2, 17, 10, Success: true,
            CriticalSuccess: false, CriticalFailure: false, "test", "strength");

        for (int i = 0; i < 5; i++)
            GameToolRegistry.ApplyToolResult(success, state);

        Assert.Equal(5, state.SuccessStreak);
        Assert.Equal(5, state.MaxStreak); // New max
    }

    [Fact]
    public void ApplyToolResult_DiceCritSuccess_AwardsCriticalHit()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(20, 0, 20, 10, Success: true,
            CriticalSuccess: true, CriticalFailure: false, "test", "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Contains("critical-hit", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_DiceCharismaSuccess_AwardsDiplomat()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(15, 1, 16, 10, Success: true,
            CriticalSuccess: false, CriticalFailure: false, "test", "charisma");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Contains("diplomat", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_DiceCharismaFailure_NoDiplomat()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(5, 1, 6, 15, Success: false,
            CriticalSuccess: false, CriticalFailure: false, "test", "charisma");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.DoesNotContain("diplomat", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_DiceStrengthSuccess_NoDiplomat()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(15, 2, 17, 10, Success: true,
            CriticalSuccess: false, CriticalFailure: false, "test", "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.DoesNotContain("diplomat", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_DiceCritFailure_ResetsStreak()
    {
        var state = new PlayerState { SuccessStreak = 3, MaxStreak = 3 };
        var dice = new DiceCheckResult(1, 0, 1, 10, Success: false,
            CriticalSuccess: false, CriticalFailure: true, "test", "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Equal(0, state.SuccessStreak);
        Assert.Equal(3, state.MaxStreak);
    }

    // ── Location Normalization ──────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_LocationResult_NormalizesToWorldMap()
    {
        var state = new PlayerState();
        // "dark forest" should normalize to "Dark Forest" from WorldMap
        GameToolRegistry.ApplyToolResult(new LocationResult("dark forest", ""), state);

        Assert.Equal("Dark Forest", state.CurrentLocation);
        Assert.Contains("Dark Forest", state.VisitedLocations);
    }

    [Fact]
    public void ApplyToolResult_LocationResult_UnknownLocation_KeptAsIs()
    {
        var state = new PlayerState();
        GameToolRegistry.ApplyToolResult(new LocationResult("Narnia", ""), state);

        Assert.Equal("Narnia", state.CurrentLocation);
    }

    [Fact]
    public void ApplyToolResult_LocationResult_SameLocationTwice_NoDuplicate()
    {
        var state = new PlayerState();
        GameToolRegistry.ApplyToolResult(new LocationResult("Dark Forest", ""), state);
        GameToolRegistry.ApplyToolResult(new LocationResult("Dark Forest", ""), state);

        // HashSet prevents duplicates
        Assert.Equal(2, state.VisitedLocations.Count); // Crossroads + Dark Forest
    }

    // ── Null/Default Handling ───────────────────────────────────────────

    [Fact]
    public void ApplyToolResult_NullResult_ReturnsNull()
    {
        var state = new PlayerState();
        var result = GameToolRegistry.ApplyToolResult(null, state);

        Assert.Null(result);
    }

    [Fact]
    public void ApplyToolResult_UnknownResultType_ReturnsAsIs()
    {
        var state = new PlayerState();
        var unknownResult = "some string";

        var result = GameToolRegistry.ApplyToolResult(unknownResult, state);

        Assert.Equal("some string", result);
    }
}
