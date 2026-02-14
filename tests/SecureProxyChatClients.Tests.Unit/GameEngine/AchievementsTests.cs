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
}
