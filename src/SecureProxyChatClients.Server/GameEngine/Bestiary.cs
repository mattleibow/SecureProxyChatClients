namespace SecureProxyChatClients.Server.GameEngine;

/// <summary>
/// A bestiary of creatures the DM can reference during encounters.
/// Demonstrates grounding AI with structured data (Monster Manual pattern).
/// </summary>
public static class Bestiary
{
    public sealed record Creature(
        string Name,
        string Emoji,
        int Level,
        int Health,
        int AttackDc,
        int Damage,
        string Description,
        string[] Abilities,
        string Weakness,
        int XpReward,
        int GoldDrop);

    public static readonly IReadOnlyList<Creature> Creatures =
    [
        new("Goblin Scout", "👺", 1, 15, 8, 3,
            "A sneaky goblin armed with a rusty dagger. Not dangerous alone, but they rarely are.",
            ["Nimble Dodge (can avoid one attack per encounter)"],
            "Fire", XpReward: 25, GoldDrop: 5),

        new("Dire Rat", "🐀", 1, 10, 6, 2,
            "An oversized rodent with glowing red eyes and yellowed fangs.",
            ["Disease Bite (10% chance to poison on hit)"],
            "Light", XpReward: 15, GoldDrop: 2),

        new("Skeleton Warrior", "💀", 2, 25, 10, 5,
            "The animated bones of a fallen soldier, wielding a notched sword.",
            ["Undead Resilience (immune to poison)", "Bone Shield (+2 to defense)"],
            "Bludgeoning", XpReward: 40, GoldDrop: 10),

        new("Shadow Wisp", "👻", 3, 20, 12, 7,
            "A writhing mass of dark energy that whispers forgotten secrets.",
            ["Incorporeal (physical attacks deal half damage)", "Life Drain (heals for damage dealt)"],
            "Radiant/Holy magic", XpReward: 60, GoldDrop: 15),

        new("Bandit Captain", "🗡️", 3, 35, 13, 8,
            "A charismatic rogue who leads a gang of cutthroats from the shadows.",
            ["Riposte (counterattack on failed enemy attack)", "Rally (buffs nearby allies)"],
            "Can be bribed or persuaded", XpReward: 75, GoldDrop: 50),

        new("Forest Troll", "🧌", 4, 60, 11, 10,
            "A hulking brute covered in moss and bark. Its wounds close almost as fast as they open.",
            ["Regeneration (recovers 5 HP per round)", "Crushing Blow (double damage on crit)"],
            "Fire (stops regeneration)", XpReward: 100, GoldDrop: 25),

        new("Crystal Golem", "💎", 5, 80, 14, 12,
            "A construct of living crystal, pulsing with arcane energy. Each facet reflects a different reality.",
            ["Magic Resistance (halves spell damage)", "Shard Burst (area attack when below half health)"],
            "Sonic/Thunder damage", XpReward: 150, GoldDrop: 40),

        new("Wraith Lord", "🦇", 6, 50, 15, 15,
            "An ancient king who refused to die. His crown still sits upon a skull wreathed in cold flame.",
            ["Dread Aura (fear check DC 14)", "Phase Walk (teleport 30ft)", "Soul Rend (ignores armor)"],
            "Sunlight (deals double damage, prevents phasing)", XpReward: 200, GoldDrop: 100),

        new("Swamp Hydra", "🐍", 7, 100, 13, 18,
            "Three serpentine heads rise from the murky water, each dripping with venom.",
            ["Multi-Attack (attacks once per head)", "Regrow Head (if not cauterized with fire)", "Venomous Bite (poison for 3 rounds)"],
            "Fire (prevents head regrowth)", XpReward: 250, GoldDrop: 60),

        new("Ancient Dragon", "🐉", 10, 200, 18, 30,
            "A mountain of scales and fury. The ground trembles with each wingbeat. Legends say it hoards not gold, but souls.",
            ["Breath Weapon (cone of fire, 40 damage)", "Frightful Presence (DC 18 fear)", "Tail Sweep (hits all adjacent)", "Legendary Resistance (auto-succeeds 3 saves per day)"],
            "Ancient rune weapons, dragonsbane herbs", XpReward: 1000, GoldDrop: 500),
    ];

    /// <summary>
    /// Get creatures appropriate for a player's level (+-2 levels).
    /// </summary>
    public static IReadOnlyList<Creature> GetCreaturesForLevel(int playerLevel)
    {
        int minLevel = Math.Max(1, playerLevel - 1);
        int maxLevel = playerLevel + 2;
        return Creatures.Where(c => c.Level >= minLevel && c.Level <= maxLevel).ToList();
    }

    /// <summary>
    /// Formats the bestiary as context for the DM system prompt.
    /// </summary>
    public static string FormatForDmPrompt(int playerLevel)
    {
        var available = GetCreaturesForLevel(playerLevel);
        if (available.Count == 0) return "";

        var lines = available.Select(c =>
            $"- {c.Emoji} {c.Name} (Lvl {c.Level}, HP {c.Health}, ATK DC {c.AttackDc}, DMG {c.Damage}, XP {c.XpReward}g {c.GoldDrop}gp): {c.Description} Abilities: {string.Join(", ", c.Abilities)}. Weakness: {c.Weakness}");

        return $"\n\nAVAILABLE CREATURES FOR ENCOUNTERS (player level {playerLevel}):\n{string.Join("\n", lines)}";
    }

    /// <summary>
    /// Pick a random creature appropriate for the player's level.
    /// </summary>
    public static Creature GetEncounterCreature(int playerLevel)
    {
        var available = GetCreaturesForLevel(playerLevel);
        return available[Random.Shared.Next(available.Count)];
    }
}
