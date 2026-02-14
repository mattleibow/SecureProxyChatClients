namespace SecureProxyChatClients.Server.GameEngine;

/// <summary>
/// Generates a text-based world map showing locations and connections.
/// Demonstrates structured data used by both server and client.
/// </summary>
public static class WorldMap
{
    public sealed record MapLocation(string Name, string Emoji, int X, int Y, string[] Connections);

    public static readonly IReadOnlyList<MapLocation> Locations =
    [
        new("The Crossroads", "✖️", 4, 4, ["Dark Forest", "Village of Thornwall", "Mountain Path", "Swamp of Sorrows"]),
        new("Dark Forest", "🌲", 2, 2, ["The Crossroads", "Ancient Temple", "Witch's Hut"]),
        new("Village of Thornwall", "🏘️", 6, 2, ["The Crossroads", "Castle Ironhold", "Market Square"]),
        new("Mountain Path", "⛰️", 4, 1, ["The Crossroads", "Dragon's Peak", "Dwarven Mines"]),
        new("Swamp of Sorrows", "🏚️", 4, 7, ["The Crossroads", "Sunken Ruins", "Witch's Hut"]),
        new("Ancient Temple", "🏛️", 1, 1, ["Dark Forest"]),
        new("Witch's Hut", "🏠", 1, 5, ["Dark Forest", "Swamp of Sorrows"]),
        new("Castle Ironhold", "🏰", 8, 1, ["Village of Thornwall"]),
        new("Market Square", "🏪", 7, 3, ["Village of Thornwall"]),
        new("Dragon's Peak", "🐉", 4, 0, ["Mountain Path"]),
        new("Dwarven Mines", "⛏️", 6, 0, ["Mountain Path"]),
        new("Sunken Ruins", "🗿", 3, 8, ["Swamp of Sorrows"]),
    ];

    /// <summary>
    /// Generate an ASCII map showing visited and unvisited locations.
    /// </summary>
    public static string GenerateMap(string currentLocation, IReadOnlySet<string> visited)
    {
        const int width = 10;
        const int height = 10;
        char[,] grid = new char[height, width * 3]; // 3 chars per cell for spacing

        // Fill with spaces
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width * 3; x++)
                grid[y, x] = ' ';

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("╔══════════════════════════════════╗");
        lines.AppendLine("║       🗺️ WORLD MAP               ║");
        lines.AppendLine("╠══════════════════════════════════╣");

        foreach (var loc in Locations)
        {
            bool isCurrent = loc.Name == currentLocation;
            bool isVisited = visited.Contains(loc.Name);
            bool isAdjacent = !isVisited && Locations
                .Where(l => visited.Contains(l.Name))
                .Any(l => l.Connections.Contains(loc.Name));

            string marker;
            if (isCurrent)
                marker = $"[{loc.Emoji}]"; // Current location in brackets
            else if (isVisited)
                marker = $" {loc.Emoji} "; // Visited
            else if (isAdjacent)
                marker = " ? "; // Adjacent but unexplored
            else
                marker = " · "; // Unknown

            string displayName = isCurrent ? $"► {loc.Name} ◄" :
                                 isVisited ? loc.Name :
                                 isAdjacent ? "???" : "";

            if (!string.IsNullOrEmpty(displayName))
                lines.AppendLine($"║ {marker} {displayName,-28} ║");
        }

        lines.AppendLine("╠══════════════════════════════════╣");
        lines.AppendLine($"║ Explored: {visited.Count}/{Locations.Count} locations          ║");
        lines.AppendLine("╚══════════════════════════════════╝");

        return lines.ToString();
    }

    /// <summary>
    /// Get available destinations from the current location.
    /// </summary>
    public static IReadOnlyList<string> GetConnections(string location)
    {
        var loc = Locations.FirstOrDefault(l => l.Name.Equals(location, StringComparison.OrdinalIgnoreCase));
        return loc?.Connections ?? [];
    }
}
