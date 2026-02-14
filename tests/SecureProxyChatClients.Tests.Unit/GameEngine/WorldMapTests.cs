using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class WorldMapTests
{
    [Fact]
    public void Locations_ContainsAtLeast12()
    {
        Assert.True(WorldMap.Locations.Count >= 12);
    }

    [Fact]
    public void AllLocations_HaveValidData()
    {
        foreach (var loc in WorldMap.Locations)
        {
            Assert.False(string.IsNullOrWhiteSpace(loc.Name));
            Assert.False(string.IsNullOrWhiteSpace(loc.Emoji));
            Assert.NotEmpty(loc.Connections);
        }
    }

    [Fact]
    public void GetConnections_Crossroads_Returns4Destinations()
    {
        var connections = WorldMap.GetConnections("The Crossroads");

        Assert.Equal(4, connections.Count);
        Assert.Contains("Dark Forest", connections);
        Assert.Contains("Village of Thornwall", connections);
        Assert.Contains("Mountain Path", connections);
        Assert.Contains("Swamp of Sorrows", connections);
    }

    [Fact]
    public void GetConnections_UnknownLocation_ReturnsEmpty()
    {
        var connections = WorldMap.GetConnections("Narnia");

        Assert.Empty(connections);
    }

    [Fact]
    public void GetConnections_CaseInsensitive()
    {
        var connections = WorldMap.GetConnections("the crossroads");

        Assert.NotEmpty(connections);
    }

    [Fact]
    public void GenerateMap_ShowsCurrentLocation()
    {
        var visited = new HashSet<string> { "The Crossroads" };
        string map = WorldMap.GenerateMap("The Crossroads", visited);

        Assert.Contains("The Crossroads", map);
        Assert.Contains("►", map); // Current location marker
    }

    [Fact]
    public void GenerateMap_ShowsExploredCount()
    {
        var visited = new HashSet<string> { "The Crossroads", "Dark Forest" };
        string map = WorldMap.GenerateMap("Dark Forest", visited);

        Assert.Contains("2/12", map);
    }

    [Fact]
    public void GenerateMap_ShowsAdjacentUnexplored()
    {
        var visited = new HashSet<string> { "The Crossroads" };
        string map = WorldMap.GenerateMap("The Crossroads", visited);

        Assert.Contains("?", map); // Unknown adjacent locations
    }

    [Fact]
    public void Connections_AreBidirectional()
    {
        // Every connection should be bidirectional
        foreach (var loc in WorldMap.Locations)
        {
            foreach (var conn in loc.Connections)
            {
                var connectedLoc = WorldMap.Locations.FirstOrDefault(l => l.Name == conn);
                Assert.NotNull(connectedLoc);
                Assert.Contains(loc.Name, connectedLoc!.Connections);
            }
        }
    }
}
