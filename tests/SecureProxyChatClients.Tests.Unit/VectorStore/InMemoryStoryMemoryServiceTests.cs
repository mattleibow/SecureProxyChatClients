using SecureProxyChatClients.Server.VectorStore;
using Microsoft.Extensions.Logging.Abstractions;

namespace SecureProxyChatClients.Tests.Unit.VectorStore;

public class InMemoryStoryMemoryServiceTests
{
    private readonly InMemoryStoryMemoryService _service = new(NullLogger<InMemoryStoryMemoryService>.Instance);

    [Fact]
    public async Task StoreMemory_ThenGetRecent_ReturnsIt()
    {
        await _service.StoreMemoryAsync("user1", "session1", "A dragon appeared", "event");

        var memories = await _service.GetRecentMemoriesAsync("user1");

        Assert.Single(memories);
        Assert.Equal("A dragon appeared", memories[0].Content);
        Assert.Equal("event", memories[0].MemoryType);
    }

    [Fact]
    public async Task GetRecent_ReturnsInReverseChronologicalOrder()
    {
        await _service.StoreMemoryAsync("user1", "s1", "First event", "event");
        await Task.Delay(10);
        await _service.StoreMemoryAsync("user1", "s1", "Second event", "event");

        var memories = await _service.GetRecentMemoriesAsync("user1", 10);

        Assert.Equal(2, memories.Count);
        Assert.Equal("Second event", memories[0].Content);
        Assert.Equal("First event", memories[1].Content);
    }

    [Fact]
    public async Task GetRecent_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _service.StoreMemoryAsync("user1", "s1", $"Event {i}", "event");

        var memories = await _service.GetRecentMemoriesAsync("user1", 2);

        Assert.Equal(2, memories.Count);
    }

    [Fact]
    public async Task GetRecent_IsolatesUsers()
    {
        await _service.StoreMemoryAsync("user1", "s1", "User1 memory", "event");
        await _service.StoreMemoryAsync("user2", "s1", "User2 memory", "event");

        var user1Memories = await _service.GetRecentMemoriesAsync("user1");
        var user2Memories = await _service.GetRecentMemoriesAsync("user2");

        Assert.Single(user1Memories);
        Assert.Equal("User1 memory", user1Memories[0].Content);
        Assert.Single(user2Memories);
        Assert.Equal("User2 memory", user2Memories[0].Content);
    }

    [Fact]
    public async Task Search_WithoutEmbeddings_FallsBackToRecent()
    {
        await _service.StoreMemoryAsync("user1", "s1", "Memory without embedding", "event");

        var results = await _service.SearchAsync("user1", [1.0f, 0.5f, 0.3f]);

        Assert.Single(results);
        Assert.Equal("Memory without embedding", results[0].Content);
    }

    [Fact]
    public async Task StoreMemory_PreservesMemoryType()
    {
        await _service.StoreMemoryAsync("user1", "s1", "Dark castle", "location");
        await _service.StoreMemoryAsync("user1", "s1", "Brave knight", "character");

        var memories = await _service.GetRecentMemoriesAsync("user1");

        Assert.Contains(memories, m => m.MemoryType == "location");
        Assert.Contains(memories, m => m.MemoryType == "character");
    }

    [Fact]
    public async Task StoreMemory_PreservesSessionId()
    {
        await _service.StoreMemoryAsync("user1", "session-abc", "Test content", "lore");

        var memories = await _service.GetRecentMemoriesAsync("user1");

        Assert.Single(memories);
        Assert.Equal("session-abc", memories[0].SessionId);
    }
}
