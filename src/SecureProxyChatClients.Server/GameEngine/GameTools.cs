using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.GameEngine;

public static class GameTools
{
    [Description("Roll dice for an action check. Returns the result and whether it succeeds against a difficulty.")]
    public static DiceCheckResult RollCheck(
        [Description("The stat being tested: strength, dexterity, wisdom, or charisma")] string stat,
        [Description("Difficulty class (1-20, where 10 is moderate)")] int difficulty,
        [Description("Brief description of what the player is attempting")] string action)
    {
        int d20 = Random.Shared.Next(1, 21);
        int modifier = stat.ToLowerInvariant() switch
        {
            "strength" => 2,
            "dexterity" => 2,
            "wisdom" => 1,
            "charisma" => 1,
            _ => 0,
        };
        int total = d20 + modifier;
        bool success = total >= difficulty;
        bool critical = d20 == 20;
        bool critFail = d20 == 1;

        return new DiceCheckResult(
            Roll: d20,
            Modifier: modifier,
            Total: total,
            Difficulty: difficulty,
            Success: critical || (success && !critFail),
            CriticalSuccess: critical,
            CriticalFailure: critFail,
            Action: action,
            Stat: stat
        );
    }

    [Description("Update the player's location. Call this when the player moves to a new area.")]
    public static LocationResult MovePlayer(
        [Description("Name of the new location")] string location,
        [Description("Brief atmospheric description of arriving")] string description)
    {
        return new LocationResult(location, description);
    }

    [Description("Add an item to the player's inventory.")]
    public static ItemResult GiveItem(
        [Description("Name of the item")] string name,
        [Description("Brief description")] string description,
        [Description("Type: weapon, armor, potion, key, or misc")] string type,
        [Description("Emoji icon for the item")] string emoji)
    {
        return new ItemResult(name, description, type, emoji, Added: true);
    }

    [Description("Remove an item from the player's inventory.")]
    public static ItemResult TakeItem(
        [Description("Name of the item to remove")] string name)
    {
        return new ItemResult(name, "", "", "", Added: false);
    }

    [Description("Deal damage to the player or heal them.")]
    public static HealthResult ModifyHealth(
        [Description("Amount to change. Positive = heal, negative = damage")] int amount,
        [Description("Source of the damage or healing")] string source)
    {
        return new HealthResult(amount, source);
    }

    [Description("Award gold to the player.")]
    public static GoldResult ModifyGold(
        [Description("Amount to change. Positive = gain, negative = spend")] int amount,
        [Description("Reason for the change")] string reason)
    {
        return new GoldResult(amount, reason);
    }

    [Description("Award experience points to the player.")]
    public static ExperienceResult AwardExperience(
        [Description("XP amount to award")] int amount,
        [Description("What the XP is for")] string reason)
    {
        return new ExperienceResult(amount, reason);
    }

    [Description("Generate an NPC with visible traits and hidden secrets the player doesn't know yet.")]
    public static NpcResult GenerateNpc(
        [Description("NPC's name")] string name,
        [Description("NPC's visible role or occupation")] string role,
        [Description("NPC's personality and appearance")] string description,
        [Description("Hidden secret the player doesn't know")] string hiddenSecret,
        [Description("NPC's attitude toward the player: friendly, neutral, hostile, or suspicious")] string attitude)
    {
        return new NpcResult(
            Id: Guid.NewGuid().ToString("N")[..8],
            Name: name,
            Role: role,
            Description: description,
            HiddenSecret: hiddenSecret,
            Attitude: attitude
        );
    }
}

// Result records
public sealed record DiceCheckResult(
    int Roll, int Modifier, int Total, int Difficulty,
    bool Success, bool CriticalSuccess, bool CriticalFailure,
    string Action, string Stat);

public sealed record LocationResult(string Location, string Description);
public sealed record ItemResult(string Name, string Description, string Type, string Emoji, bool Added);
public sealed record HealthResult(int Amount, string Source);
public sealed record GoldResult(int Amount, string Reason);
public sealed record ExperienceResult(int Amount, string Reason);

public sealed record NpcResult(
    string Id, string Name, string Role, string Description,
    string HiddenSecret, string Attitude);
