namespace SecureProxyChatClients.Server.GameEngine;

public sealed class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string Name { get; set; } = "Adventurer";
    public string CharacterClass { get; set; } = "Explorer";
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Gold { get; set; } = 10;
    public int Experience { get; set; }
    public int Level { get; set; } = 1;
    public string CurrentLocation { get; set; } = "The Crossroads";
    public List<InventoryItem> Inventory { get; set; } = [];
    public Dictionary<string, int> Stats { get; set; } = new()
    {
        ["strength"] = 10,
        ["dexterity"] = 10,
        ["wisdom"] = 10,
        ["charisma"] = 10,
    };
    public HashSet<string> VisitedLocations { get; set; } = ["The Crossroads"];
    public HashSet<string> UnlockedAchievements { get; set; } = [];
    public int Version { get; set; }
}

public sealed class InventoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = "📦";
    public string Type { get; set; } = "misc"; // weapon, armor, potion, key, misc
    public int Quantity { get; set; } = 1;
}
