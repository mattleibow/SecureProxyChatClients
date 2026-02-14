using System.Collections.Concurrent;

namespace SecureProxyChatClients.Server.GameEngine;

public interface IGameStateStore
{
    Task<PlayerState> GetOrCreatePlayerStateAsync(string userId, CancellationToken ct = default);
    Task SavePlayerStateAsync(string userId, PlayerState state, CancellationToken ct = default);
}

public sealed class InMemoryGameStateStore : IGameStateStore
{
    private readonly ConcurrentDictionary<string, PlayerState> _states = new();

    public Task<PlayerState> GetOrCreatePlayerStateAsync(string userId, CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(userId, id => new PlayerState { PlayerId = id });
        return Task.FromResult(state);
    }

    public Task SavePlayerStateAsync(string userId, PlayerState state, CancellationToken ct = default)
    {
        _states[userId] = state;
        return Task.CompletedTask;
    }
}
