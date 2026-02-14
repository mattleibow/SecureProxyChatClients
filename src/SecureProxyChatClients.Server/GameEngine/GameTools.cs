using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.GameEngine;

public static class GameTools
{
    private static readonly HashSet<string> ValidStats = ["strength", "dexterity", "wisdom", "charisma"];
    private static readonly HashSet<string> ValidItemTypes = ["weapon", "armor", "potion", "key", "misc"];
    private static readonly HashSet<string> ValidRarities = ["common", "uncommon", "rare", "epic", "legendary"];
    private static readonly HashSet<string> ValidAttitudes = ["friendly", "neutral", "hostile", "suspicious"];
    private const int MaxStringLength = 500;

    private static string Clamp(string? value, string fallback, int maxLen = MaxStringLength) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Length > maxLen ? value[..maxLen] : value;

    [Description("Roll dice for an action check. Returns the result and whether it succeeds against a difficulty.")]
    public static DiceCheckResult RollCheck(
        [Description("The stat being tested: strength, dexterity, wisdom, or charisma")] string stat,
        [Description("Difficulty class (1-20, where 10 is moderate)")] int difficulty,
        [Description("Brief description of what the player is attempting")] string action,
        [Description("The player's current value for the stat being tested (e.g., 14 for STR 14). Used to calculate the modifier.")] int statValue = 10)
    {
        stat = Clamp(stat, "strength").ToLowerInvariant();
        difficulty = Math.Clamp(difficulty, 1, 30);
        action = Clamp(action, "an action");
        statValue = Math.Clamp(statValue, 1, 30);

        int d20 = Random.Shared.Next(1, 21);
        // D&D-style modifier: (stat - 10) / 2, so STR 14 → +2, STR 10 → +0, STR 8 → -1
        int modifier = (statValue - 10) / 2;
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
        return new LocationResult(Clamp(location, "Unknown"), Clamp(description, "You arrive."));
    }

    [Description("Add an item to the player's inventory.")]
    public static ItemResult GiveItem(
        [Description("Name of the item")] string name,
        [Description("Brief description")] string description,
        [Description("Type: weapon, armor, potion, key, or misc")] string type,
        [Description("Emoji icon for the item")] string emoji,
        [Description("Rarity: common, uncommon, rare, epic, or legendary")] string rarity = "common")
    {
        type = Clamp(type, "misc").ToLowerInvariant();
        if (!ValidItemTypes.Contains(type)) type = "misc";
        rarity = Clamp(rarity, "common").ToLowerInvariant();
        if (!ValidRarities.Contains(rarity)) rarity = "common";

        return new ItemResult(Clamp(name, "Item"), Clamp(description, ""), type, Clamp(emoji, "📦", 10), rarity, Added: true);
    }

    [Description("Remove an item from the player's inventory.")]
    public static ItemResult TakeItem(
        [Description("Name of the item to remove")] string name)
    {
        return new ItemResult(Clamp(name, "Item"), "", "", "", "common", Added: false);
    }

    [Description("Deal damage to the player or heal them.")]
    public static HealthResult ModifyHealth(
        [Description("Amount to change. Positive = heal, negative = damage")] int amount,
        [Description("Source of the damage or healing")] string source)
    {
        amount = Math.Clamp(amount, -200, 200);
        return new HealthResult(amount, Clamp(source, "unknown"));
    }

    [Description("Award gold to the player.")]
    public static GoldResult ModifyGold(
        [Description("Amount to change. Positive = gain, negative = spend")] int amount,
        [Description("Reason for the change")] string reason)
    {
        amount = Math.Clamp(amount, -1000, 1000);
        return new GoldResult(amount, Clamp(reason, "unknown"));
    }

    [Description("Award experience points to the player.")]
    public static ExperienceResult AwardExperience(
        [Description("XP amount to award")] int amount,
        [Description("What the XP is for")] string reason)
    {
        amount = Math.Clamp(amount, 0, 5000);
        return new ExperienceResult(amount, Clamp(reason, "unknown"));
    }

    [Description("Generate an NPC with visible traits and hidden secrets the player doesn't know yet.")]
    public static NpcResult GenerateNpc(
        [Description("NPC's name")] string name,
        [Description("NPC's visible role or occupation")] string role,
        [Description("NPC's personality and appearance")] string description,
        [Description("Hidden secret the player doesn't know")] string hiddenSecret,
        [Description("NPC's attitude toward the player: friendly, neutral, hostile, or suspicious")] string attitude)
    {
        attitude = Clamp(attitude, "neutral").ToLowerInvariant();
        if (!ValidAttitudes.Contains(attitude)) attitude = "neutral";

        return new NpcResult(
            Id: Guid.NewGuid().ToString("N")[..8],
            Name: Clamp(name, "Stranger"),
            Role: Clamp(role, "unknown"),
            Description: Clamp(description, "A mysterious figure."),
            HiddenSecret: Clamp(hiddenSecret, "None"),
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
public sealed record ItemResult(string Name, string Description, string Type, string Emoji, string Rarity, bool Added);
public sealed record HealthResult(int Amount, string Source);
public sealed record GoldResult(int Amount, string Reason);
public sealed record ExperienceResult(int Amount, string Reason);

public sealed record NpcResult(
    string Id, string Name, string Role, string Description,
    string HiddenSecret, string Attitude);
