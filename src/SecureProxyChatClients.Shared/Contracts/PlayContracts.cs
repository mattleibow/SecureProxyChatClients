using System.Text.Json;

namespace SecureProxyChatClients.Shared.Contracts;

public sealed record PlayResponse
{
    public required List<ChatMessageDto> Messages { get; init; }
    public string? SessionId { get; init; }
    public object? PlayerState { get; init; }
    public List<GameEvent> GameEvents { get; init; } = [];
}

public sealed record GameEvent
{
    public required string Type { get; init; }
    public JsonElement? Data { get; init; }
}

public sealed record PlayerStateDto
{
    public string Name { get; init; } = "Adventurer";
    public string CharacterClass { get; init; } = "Explorer";
    public int Health { get; init; } = 100;
    public int MaxHealth { get; init; } = 100;
    public int Gold { get; init; } = 10;
    public int Experience { get; init; }
    public int Level { get; init; } = 1;
    public string CurrentLocation { get; init; } = "The Crossroads";
    public List<InventoryItemDto> Inventory { get; init; } = [];
    public Dictionary<string, int> Stats { get; init; } = [];
    public HashSet<string> UnlockedAchievements { get; init; } = [];
    public HashSet<string> VisitedLocations { get; init; } = [];
}

public sealed record InventoryItemDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Emoji { get; init; } = "📦";
    public string Type { get; init; } = "misc";
    public int Quantity { get; init; } = 1;
}
