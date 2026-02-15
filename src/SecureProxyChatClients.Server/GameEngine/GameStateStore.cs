using System.Collections.Concurrent;

namespace SecureProxyChatClients.Server.GameEngine;

public interface IGameStateStore
{
    Task<PlayerState> GetOrCreatePlayerStateAsync(string userId, CancellationToken ct = default);
    Task SavePlayerStateAsync(string userId, PlayerState state, CancellationToken ct = default);
    Task ResetPlayerStateAsync(string userId, PlayerState newState, CancellationToken ct = default);
}

public sealed class InMemoryGameStateStore : IGameStateStore
{
    private readonly ConcurrentDictionary<string, PlayerState> _states = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<PlayerState> GetOrCreatePlayerStateAsync(string userId, CancellationToken ct = default)
    {
        // Get or create lock for user
        var userLock = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);
        
        try
        {
            var state = _states.GetOrAdd(userId, id => new PlayerState { PlayerId = id });
            // Return a deep copy to prevent shared reference issues if we weren't locking,
            // but since we are locking the *entire operation* from Get to Save in the endpoint (if we change the endpoint),
            // we should be careful.
            // Actually, the endpoint pattern is Get -> Modify -> Save.
            // If we lock here, we return the state but release the lock? No.
            // The lock needs to be held *during* the modification.
            // This interface is hard to make thread-safe without changing the contract to `UpdateStateAsync(userId, func)`.
            // For this exercise, I will change the store to return a COPY, so modifications don't affect the store until Save is called.
            // And Save should check a version/etag.
            // But PlayerState doesn't have a version.
            // Let's at least return a copy to avoid reference sharing issues.
            return Clone(state);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task SavePlayerStateAsync(string userId, PlayerState newState, CancellationToken ct = default)
    {
        var userLock = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);
        
        try
        {
            if (_states.TryGetValue(userId, out var existingState))
            {
                if (newState.Version != existingState.Version)
                {
                    throw new InvalidOperationException("Concurrency conflict: The game state has been modified by another request.");
                }
            }
            
            newState.Version++;
            _states[userId] = Clone(newState);
        }
        finally
        {
            userLock.Release();
        }
    }

    private static PlayerState Clone(PlayerState source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<PlayerState>(json)!;
    }

    /// <summary>
    /// Replaces player state unconditionally (bypasses version check). Used for new game / reset.
    /// </summary>
    public async Task ResetPlayerStateAsync(string userId, PlayerState newState, CancellationToken ct = default)
    {
        var userLock = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);

        try
        {
            newState.Version = 1;
            _states[userId] = Clone(newState);
        }
        finally
        {
            userLock.Release();
        }
    }
}
