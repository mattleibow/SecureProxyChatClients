using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class BestiaryTests
{
    [Fact]
    public void Creatures_ContainsAtLeast10Entries()
    {
        Assert.True(Bestiary.Creatures.Count >= 10);
    }

    [Fact]
    public void AllCreatures_HaveValidData()
    {
        foreach (var creature in Bestiary.Creatures)
        {
            Assert.False(string.IsNullOrWhiteSpace(creature.Name));
            Assert.False(string.IsNullOrWhiteSpace(creature.Emoji));
            Assert.True(creature.Level > 0);
            Assert.True(creature.Health > 0);
            Assert.True(creature.AttackDc > 0);
            Assert.True(creature.Damage > 0);
            Assert.False(string.IsNullOrWhiteSpace(creature.Description));
            Assert.NotEmpty(creature.Abilities);
            Assert.False(string.IsNullOrWhiteSpace(creature.Weakness));
            Assert.True(creature.XpReward > 0);
            Assert.True(creature.GoldDrop >= 0);
        }
    }

    [Theory]
    [InlineData(1, "Goblin Scout")]
    [InlineData(1, "Dire Rat")]
    [InlineData(10, "Ancient Dragon")]
    public void GetCreaturesForLevel_ReturnsExpectedCreature(int level, string expectedName)
    {
        var creatures = Bestiary.GetCreaturesForLevel(level);
        Assert.Contains(creatures, c => c.Name == expectedName);
    }

    [Fact]
    public void GetCreaturesForLevel_ExcludesHighLevelCreatures()
    {
        var level1Creatures = Bestiary.GetCreaturesForLevel(1);
        Assert.DoesNotContain(level1Creatures, c => c.Name == "Ancient Dragon");
    }

    [Fact]
    public void FormatForDmPrompt_IncludesCreatureDetails()
    {
        string prompt = Bestiary.FormatForDmPrompt(1);

        Assert.Contains("Goblin Scout", prompt);
        Assert.Contains("AVAILABLE CREATURES", prompt);
        Assert.Contains("Weakness:", prompt);
    }

    [Fact]
    public void FormatForDmPrompt_ScalesToPlayerLevel()
    {
        string lowPrompt = Bestiary.FormatForDmPrompt(1);
        string highPrompt = Bestiary.FormatForDmPrompt(8);

        Assert.DoesNotContain("Ancient Dragon", lowPrompt);
        Assert.Contains("Ancient Dragon", highPrompt);
    }

    [Fact]
    public void Creatures_LevelsAreNonDecreasing()
    {
        int prevLevel = 0;
        foreach (var creature in Bestiary.Creatures)
        {
            Assert.True(creature.Level >= prevLevel,
                $"{creature.Name} (level {creature.Level}) is out of order after level {prevLevel}");
            prevLevel = creature.Level;
        }
    }

    [Fact]
    public void GetEncounterCreature_ReturnsCreatureWithinLevelRange()
    {
        var creature = Bestiary.GetEncounterCreature(3);
        Assert.InRange(creature.Level, 2, 5);
    }

    [Fact]
    public void GetEncounterCreature_AtLevel1_ReturnsLowLevelCreature()
    {
        var creature = Bestiary.GetEncounterCreature(1);
        Assert.InRange(creature.Level, 1, 3);
    }
}
