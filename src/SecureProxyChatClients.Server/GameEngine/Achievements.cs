namespace SecureProxyChatClients.Server.GameEngine;

/// <summary>
/// Achievement system that tracks player milestones.
/// Achievements are checked after each game action and awarded automatically.
/// </summary>
public static class Achievements
{
    public sealed record Achievement(string Id, string Title, string Description, string Emoji, string Category);

    public static readonly IReadOnlyList<Achievement> All =
    [
        // Combat
        new("first-blood", "First Blood", "Win your first combat encounter", "⚔️", "combat"),
        new("critical-hit", "Critical Hit", "Roll a natural 20", "🎯", "combat"),
        new("survivor", "Survivor", "Survive a fight with less than 5 HP", "💪", "combat"),
        new("dragon-slayer", "Dragon Slayer", "Defeat an Ancient Dragon", "🐉", "combat"),

        // Exploration
        new("first-steps", "First Steps", "Move to a new location", "👣", "exploration"),
        new("explorer", "World Walker", "Visit 5 different locations", "🗺️", "exploration"),
        new("cartographer", "Cartographer", "Visit 10 different locations", "🧭", "exploration"),

        // Social
        new("first-contact", "First Contact", "Meet your first NPC", "🤝", "social"),
        new("diplomat", "Silver Tongue", "Succeed at a charisma check", "🗣️", "social"),
        new("secret-keeper", "Secret Keeper", "Discover an NPC's hidden secret", "🤫", "social"),

        // Wealth
        new("first-loot", "Loot Goblin", "Find your first item", "📦", "wealth"),
        new("hoarder", "Hoarder", "Have 10 or more items", "🎒", "wealth"),
        new("wealthy", "Wealthy", "Accumulate 100 gold", "💰", "wealth"),
        new("rich", "Filthy Rich", "Accumulate 500 gold", "👑", "wealth"),

        // Progression
        new("level-2", "Getting Stronger", "Reach level 2", "⬆️", "progression"),
        new("level-5", "Seasoned Adventurer", "Reach level 5", "🌟", "progression"),
        new("level-10", "Legend", "Reach level 10", "✨", "progression"),
        new("twist-of-fate", "Tempting Fate", "Trigger a Twist of Fate", "🌀", "progression"),
    ];

    /// <summary>
    /// Check which achievements the player has earned based on their state.
    /// Returns only newly earned achievements (not already in the unlocked list).
    /// </summary>
    public static IReadOnlyList<Achievement> CheckAchievements(PlayerState state, IReadOnlySet<string> unlockedIds)
    {
        List<Achievement> newlyEarned = [];

        foreach (var achievement in All)
        {
            if (unlockedIds.Contains(achievement.Id)) continue;

            bool earned = achievement.Id switch
            {
                "first-steps" => state.CurrentLocation != "The Crossroads",
                "explorer" => state.VisitedLocations.Count >= 5,
                "cartographer" => state.VisitedLocations.Count >= 10,
                "first-loot" => state.Inventory.Count > 3, // Characters start with 3 items; > 3 means they found loot
                "hoarder" => state.Inventory.Sum(i => i.Quantity) >= 10,
                "wealthy" => state.Gold >= 100,
                "rich" => state.Gold >= 500,
                "level-2" => state.Level >= 2,
                "level-5" => state.Level >= 5,
                "level-10" => state.Level >= 10,
                _ => false, // Event-based achievements are awarded by game events, not state checks
            };

            if (earned) newlyEarned.Add(achievement);
        }

        return newlyEarned;
    }

    /// <summary>
    /// Get an achievement by its ID (for event-based awarding).
    /// </summary>
    public static Achievement? GetById(string id) => All.FirstOrDefault(a => a.Id == id);
}
