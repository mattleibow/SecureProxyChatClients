using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class AchievementsTests
{
    [Fact]
    public void All_ContainsAtLeast18Achievements()
    {
        Assert.True(Achievements.All.Count >= 18);
    }

    [Fact]
    public void All_HaveUniqueIds()
    {
        var ids = Achievements.All.Select(a => a.Id).ToHashSet();
        Assert.Equal(Achievements.All.Count, ids.Count);
    }

    [Fact]
    public void CheckAchievements_NewCharacter_NoAchievements()
    {
        var state = new PlayerState();
        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void CheckAchievements_MovedLocation_EarnsFirstSteps()
    {
        var state = new PlayerState { CurrentLocation = "Dark Forest" };
        state.VisitedLocations.Add("Dark Forest");

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "first-steps");
    }

    [Fact]
    public void CheckAchievements_Level2_EarnsGettingStronger()
    {
        var state = new PlayerState { Level = 2 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "level-2");
    }

    [Fact]
    public void CheckAchievements_100Gold_EarnsWealthy()
    {
        var state = new PlayerState { Gold = 100 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "wealthy");
    }

    [Fact]
    public void CheckAchievements_DoesNotReawardUnlockedAchievements()
    {
        var state = new PlayerState { Level = 2 };
        HashSet<string> unlocked = ["level-2"];

        var result = Achievements.CheckAchievements(state, unlocked);

        Assert.DoesNotContain(result, a => a.Id == "level-2");
    }

    [Fact]
    public void CheckAchievements_5Locations_EarnsExplorer()
    {
        var state = new PlayerState();
        for (int i = 0; i < 5; i++)
            state.VisitedLocations.Add($"Location {i}");
        state.CurrentLocation = "Location 4";

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "explorer");
    }

    [Fact]
    public void GetById_ValidId_ReturnsAchievement()
    {
        var ach = Achievements.GetById("first-blood");

        Assert.NotNull(ach);
        Assert.Equal("First Blood", ach.Title);
    }

    [Fact]
    public void GetById_InvalidId_ReturnsNull()
    {
        var ach = Achievements.GetById("nonexistent");

        Assert.Null(ach);
    }

    [Fact]
    public void CheckAchievements_500Gold_EarnsRich()
    {
        var state = new PlayerState { Gold = 500 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "rich");
        Assert.Contains(result, a => a.Id == "wealthy"); // Also gets wealthy
    }

    [Fact]
    public void CheckAchievements_10Locations_EarnsCartographer()
    {
        var state = new PlayerState();
        for (int i = 0; i < 10; i++)
            state.VisitedLocations.Add($"Location {i}");
        state.CurrentLocation = "Location 9";

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "cartographer");
    }

    [Fact]
    public void CheckAchievements_4Items_EarnsFirstLoot()
    {
        // "first-loot" triggers when Inventory.Count > 3 (chars start with 3 items)
        var state = new PlayerState();
        for (int i = 0; i < 4; i++)
            state.Inventory.Add(new InventoryItem { Name = $"Item {i}" });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "first-loot");
    }

    [Fact]
    public void CheckAchievements_3Items_NoFirstLoot()
    {
        // Characters start with 3 items, so 3 items should NOT trigger first-loot
        var state = new PlayerState();
        for (int i = 0; i < 3; i++)
            state.Inventory.Add(new InventoryItem { Name = $"Item {i}" });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "first-loot");
    }

    [Fact]
    public void CheckAchievements_10Items_EarnsHoarder()
    {
        // "hoarder" triggers when Inventory.Sum(i => i.Quantity) >= 10
        var state = new PlayerState();
        for (int i = 0; i < 10; i++)
            state.Inventory.Add(new InventoryItem { Name = $"Item {i}", Quantity = 1 });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "hoarder");
    }

    [Fact]
    public void CheckAchievements_Level5_EarnsSeasoned()
    {
        var state = new PlayerState { Level = 5 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "level-5");
    }

    [Fact]
    public void CheckAchievements_Level10_EarnsLegend()
    {
        var state = new PlayerState { Level = 10 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "level-10");
    }

    [Fact]
    public void ApplyToolResult_CombatResult_AwardsFirstBlood()
    {
        var state = new PlayerState { Health = 50 };
        var win = new CombatResult("Goblin Scout");

        GameToolRegistry.ApplyToolResult(win, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_CombatResult_LowHealth_AwardsSurvivor()
    {
        var state = new PlayerState { Health = 3 };
        var win = new CombatResult("Bandit Captain");

        GameToolRegistry.ApplyToolResult(win, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
        Assert.Contains("survivor", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_AncientDragonKill_AwardsDragonSlayer()
    {
        var state = new PlayerState { Health = 20 };
        var combat = new CombatResult("Ancient Dragon");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.Contains("dragon-slayer", state.UnlockedAchievements);
        Assert.Contains("first-blood", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_NpcWithSecret_AwardsSecretKeeper()
    {
        var state = new PlayerState();
        var npc = new NpcResult("npc1", "Eva", "Scholar", "Wise woman", "Secretly a spy", "neutral");

        GameToolRegistry.ApplyToolResult(npc, state);

        Assert.Contains("secret-keeper", state.UnlockedAchievements);
        Assert.Contains("first-contact", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_NpcWithoutSecret_NoSecretKeeper()
    {
        var state = new PlayerState();
        var npc = new NpcResult("npc2", "Bob", "Guard", "A guard", "None", "friendly");

        GameToolRegistry.ApplyToolResult(npc, state);

        Assert.DoesNotContain("secret-keeper", state.UnlockedAchievements);
        Assert.Contains("first-contact", state.UnlockedAchievements);
    }

    // ── Achievement Boundary Tests ──────────────────────────────────────

    [Fact]
    public void CheckAchievements_4Locations_NoExplorer()
    {
        // PlayerState starts with "The Crossroads" in VisitedLocations
        // So we need 3 more (total 4) to NOT trigger explorer (requires 5)
        var state = new PlayerState();
        for (int i = 0; i < 3; i++)
            state.VisitedLocations.Add($"Location {i}");
        state.CurrentLocation = "Location 2";

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "explorer");
    }

    [Fact]
    public void CheckAchievements_9Locations_NoCartographer()
    {
        // PlayerState starts with "The Crossroads" (1), add 8 more = 9 total
        var state = new PlayerState();
        for (int i = 0; i < 8; i++)
            state.VisitedLocations.Add($"Location {i}");
        state.CurrentLocation = "Location 7";

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "cartographer");
        Assert.Contains(result, a => a.Id == "explorer"); // 9 >= 5
    }

    [Fact]
    public void CheckAchievements_99Gold_NoWealthy()
    {
        var state = new PlayerState { Gold = 99 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "wealthy");
    }

    [Fact]
    public void CheckAchievements_499Gold_NoRich()
    {
        var state = new PlayerState { Gold = 499 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "rich");
        Assert.Contains(result, a => a.Id == "wealthy"); // 499 >= 100
    }

    [Fact]
    public void CheckAchievements_HoarderWithQuantityItems()
    {
        // 3 items with quantity 4, 3, 3 = sum 10
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Arrows", Quantity = 4 });
        state.Inventory.Add(new InventoryItem { Name = "Potions", Quantity = 3 });
        state.Inventory.Add(new InventoryItem { Name = "Gems", Quantity = 3 });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.Contains(result, a => a.Id == "hoarder");
    }

    [Fact]
    public void CheckAchievements_HoarderQuantitySum9_NoHoarder()
    {
        var state = new PlayerState();
        state.Inventory.Add(new InventoryItem { Name = "Arrows", Quantity = 5 });
        state.Inventory.Add(new InventoryItem { Name = "Potions", Quantity = 4 });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "hoarder");
    }

    [Fact]
    public void ApplyToolResult_SurvivorAtExactly5HP_NotAwarded()
    {
        // Survivor requires Health < 5, not <=
        var state = new PlayerState { Health = 5 };
        var combat = new CombatResult("Goblin");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
        Assert.DoesNotContain("survivor", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_SurvivorAtExactly1HP_Awarded()
    {
        var state = new PlayerState { Health = 1 };
        var combat = new CombatResult("Goblin");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.Contains("survivor", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_CombatAtZeroHP_NoSurvivor()
    {
        // Health must be > 0 for survivor
        var state = new PlayerState { Health = 0 };
        var combat = new CombatResult("Goblin");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.Contains("first-blood", state.UnlockedAchievements);
        Assert.DoesNotContain("survivor", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_NonDragonCombat_NoDragonSlayer()
    {
        var state = new PlayerState { Health = 50 };
        var combat = new CombatResult("Forest Wolf");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.DoesNotContain("dragon-slayer", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_DragonSlayerCaseInsensitive()
    {
        var state = new PlayerState { Health = 50 };
        var combat = new CombatResult("ancient dragon");

        GameToolRegistry.ApplyToolResult(combat, state);

        Assert.Contains("dragon-slayer", state.UnlockedAchievements);
    }

    [Fact]
    public void CheckAchievements_Level1_NoLevelAchievement()
    {
        var state = new PlayerState { Level = 1 };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "level-2");
        Assert.DoesNotContain(result, a => a.Id == "level-5");
        Assert.DoesNotContain(result, a => a.Id == "level-10");
    }

    [Fact]
    public void CheckAchievements_AtCrossroads_NoFirstSteps()
    {
        var state = new PlayerState { CurrentLocation = "The Crossroads" };

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        Assert.DoesNotContain(result, a => a.Id == "first-steps");
    }
}
