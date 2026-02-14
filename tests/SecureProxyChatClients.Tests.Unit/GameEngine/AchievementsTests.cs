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
    public void CheckAchievements_MultipleAtOnce()
    {
        var state = new PlayerState
        {
            Level = 10,
            Gold = 500,
            CurrentLocation = "Somewhere",
        };
        for (int i = 0; i < 10; i++)
            state.VisitedLocations.Add($"Location {i}");
        for (int i = 0; i < 10; i++)
            state.Inventory.Add(new InventoryItem { Name = $"Item {i}", Quantity = 1 });

        var result = Achievements.CheckAchievements(state, new HashSet<string>());

        // Should earn multiple achievements at once
        Assert.Contains(result, a => a.Id == "level-10");
        Assert.Contains(result, a => a.Id == "level-5");
        Assert.Contains(result, a => a.Id == "level-2");
        Assert.Contains(result, a => a.Id == "rich");
        Assert.Contains(result, a => a.Id == "wealthy");
        Assert.Contains(result, a => a.Id == "cartographer");
        Assert.Contains(result, a => a.Id == "explorer");
        Assert.Contains(result, a => a.Id == "first-steps");
        Assert.Contains(result, a => a.Id == "hoarder");
        Assert.Contains(result, a => a.Id == "first-loot");
    }

    [Fact]
    public void ApplyToolResult_CriticalSuccess_AwardsCriticalHitAchievement()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(
            Roll: 20, Modifier: 2, Total: 22, Difficulty: 10,
            Success: true, CriticalSuccess: true, CriticalFailure: false,
            Action: "attack", Stat: "strength");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Contains("critical-hit", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_CharismaSuccess_AwardsDiplomatAchievement()
    {
        var state = new PlayerState();
        var dice = new DiceCheckResult(
            Roll: 15, Modifier: 1, Total: 16, Difficulty: 10,
            Success: true, CriticalSuccess: false, CriticalFailure: false,
            Action: "persuade", Stat: "charisma");

        GameToolRegistry.ApplyToolResult(dice, state);

        Assert.Contains("diplomat", state.UnlockedAchievements);
    }

    [Fact]
    public void ApplyToolResult_NpcResult_AwardsFirstContactAchievement()
    {
        var state = new PlayerState();
        var npc = new NpcResult("npc1", "Eva", "Scholar", "Wise woman", "Secretly a spy", "neutral");

        GameToolRegistry.ApplyToolResult(npc, state);

        Assert.Contains("first-contact", state.UnlockedAchievements);
    }

    [Fact]
    public void CheckCombatAchievements_CombatWon_AwardsFirstBlood()
    {
        var state = new PlayerState { Health = 50 };

        GameToolRegistry.CheckCombatAchievements(state, combatWon: true);

        Assert.Contains("first-blood", state.UnlockedAchievements);
    }

    [Fact]
    public void CheckCombatAchievements_LowHealthWin_AwardsSurvivor()
    {
        var state = new PlayerState { Health = 3 };

        GameToolRegistry.CheckCombatAchievements(state, combatWon: true);

        Assert.Contains("first-blood", state.UnlockedAchievements);
        Assert.Contains("survivor", state.UnlockedAchievements);
    }
}
