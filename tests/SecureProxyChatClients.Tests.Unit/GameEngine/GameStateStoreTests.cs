using SecureProxyChatClients.Server.GameEngine;

namespace SecureProxyChatClients.Tests.Unit.GameEngine;

public class GameStateStoreTests
{
    [Fact]
    public async Task GetOrCreate_ReturnsNewStateForNewUser()
    {
        var store = new InMemoryGameStateStore();

        var state = await store.GetOrCreatePlayerStateAsync("user1");

        Assert.Equal("user1", state.PlayerId);
        Assert.Equal("Adventurer", state.Name);
        Assert.Equal(100, state.Health);
    }

    [Fact]
    public async Task GetOrCreate_ReturnsSameStateForSameUser()
    {
        var store = new InMemoryGameStateStore();

        var state1 = await store.GetOrCreatePlayerStateAsync("user1");
        state1.Name = "Hero";
        await store.SavePlayerStateAsync("user1", state1);
        var state2 = await store.GetOrCreatePlayerStateAsync("user1");

        Assert.Equal("Hero", state2.Name);
    }

    [Fact]
    public async Task Save_PersistsState()
    {
        var store = new InMemoryGameStateStore();
        var state = new PlayerState { PlayerId = "user1", Name = "TestHero", Gold = 999 };

        await store.SavePlayerStateAsync("user1", state);
        var retrieved = await store.GetOrCreatePlayerStateAsync("user1");

        Assert.Equal("TestHero", retrieved.Name);
        Assert.Equal(999, retrieved.Gold);
    }

    [Fact]
    public async Task DifferentUsers_HaveIsolatedState()
    {
        var store = new InMemoryGameStateStore();

        var state1 = await store.GetOrCreatePlayerStateAsync("user1");
        state1.Name = "Hero1";

        var state2 = await store.GetOrCreatePlayerStateAsync("user2");

        Assert.Equal("Adventurer", state2.Name);
        Assert.NotEqual(state1.PlayerId, state2.PlayerId);
    }

    [Fact]
    public async Task ResetPlayerState_OverwritesExistingState()
    {
        var store = new InMemoryGameStateStore();

        // Create and save initial state
        var state = await store.GetOrCreatePlayerStateAsync("user1");
        state.Name = "OldHero";
        state.Level = 5;
        state.Gold = 500;
        await store.SavePlayerStateAsync("user1", state);

        // Reset with a fresh state (simulates new game)
        var newState = new PlayerState { PlayerId = "user1", Name = "NewHero" };
        await store.ResetPlayerStateAsync("user1", newState);

        var retrieved = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal("NewHero", retrieved.Name);
        Assert.Equal(1, retrieved.Level);
        Assert.Equal(10, retrieved.Gold); // Default starting gold
    }

    [Fact]
    public async Task ResetPlayerState_AllowsSubsequentSave()
    {
        var store = new InMemoryGameStateStore();

        // Save initial state
        var state = new PlayerState { PlayerId = "user1", Name = "Hero" };
        await store.ResetPlayerStateAsync("user1", state);

        // Modify and save
        var current = await store.GetOrCreatePlayerStateAsync("user1");
        current.Gold = 50;
        await store.SavePlayerStateAsync("user1", current);

        // Reset again (should not throw version conflict)
        var resetState = new PlayerState { PlayerId = "user1", Name = "NewGame" };
        await store.ResetPlayerStateAsync("user1", resetState);

        var final = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal("NewGame", final.Name);
        Assert.Equal(10, final.Gold); // Default starting gold
    }

    [Fact]
    public async Task SavePlayerState_ConcurrencyConflict_Throws()
    {
        var store = new InMemoryGameStateStore();

        // Get state (version 0 in store)
        var stateA = await store.GetOrCreatePlayerStateAsync("user1");
        var stateB = await store.GetOrCreatePlayerStateAsync("user1");

        // Save first copy (version 0 → 1 in store)
        stateA.Gold = 100;
        await store.SavePlayerStateAsync("user1", stateA);

        // Save second copy (still version 0, but store is now version 1)
        stateB.Gold = 200;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SavePlayerStateAsync("user1", stateB));
    }

    [Fact]
    public async Task SavePlayerState_VersionIncrements()
    {
        var store = new InMemoryGameStateStore();

        var state = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal(0, state.Version);

        await store.SavePlayerStateAsync("user1", state);
        var after = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal(1, after.Version);

        await store.SavePlayerStateAsync("user1", after);
        var afterSecond = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal(2, afterSecond.Version);
    }

    [Fact]
    public async Task GetOrCreate_ReturnsCopy_NotReference()
    {
        var store = new InMemoryGameStateStore();

        var state1 = await store.GetOrCreatePlayerStateAsync("user1");
        state1.Gold = 999;
        // Don't save — changes should not affect the store

        var state2 = await store.GetOrCreatePlayerStateAsync("user1");
        Assert.Equal(10, state2.Gold); // Default, not 999
    }
}
