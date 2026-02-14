using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Data;

public class EfConversationStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly EfConversationStore _store;

    public EfConversationStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _store = new EfConversationStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateSession_ReturnsNonEmptyId()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        Assert.False(string.IsNullOrEmpty(sessionId));
    }

    [Fact]
    public async Task CreateSession_PersistsToDatabase()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        var session = await _db.ConversationSessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal("user1", session.UserId);
    }

    [Fact]
    public async Task AppendMessages_StoresMessages()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "user", Content = "Hello" },
            new ChatMessageDto { Role = "assistant", Content = "Hi there!" },
        ]);

        var history = await _store.GetHistoryAsync(sessionId);
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("Hello", history[0].Content);
        Assert.Equal("assistant", history[1].Role);
        Assert.Equal("Hi there!", history[1].Content);
    }

    [Fact]
    public async Task AppendMessages_SetsAutoTitle()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "user", Content = "What is the weather?" },
        ]);

        var session = await _db.ConversationSessions.FindAsync(sessionId);
        Assert.Equal("What is the weather?", session!.Title);
    }

    [Fact]
    public async Task AppendMessages_TruncatesLongTitle()
    {
        string sessionId = await _store.CreateSessionAsync("user1");
        string longContent = new string('x', 200);

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "user", Content = longContent },
        ]);

        var session = await _db.ConversationSessions.FindAsync(sessionId);
        Assert.True(session!.Title!.Length <= 81); // 80 + ellipsis
    }

    [Fact]
    public async Task AppendMessages_PreservesSequenceOrder()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "user", Content = "First" },
        ]);

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "assistant", Content = "Second" },
            new ChatMessageDto { Role = "user", Content = "Third" },
        ]);

        var history = await _store.GetHistoryAsync(sessionId);
        Assert.Equal(3, history.Count);
        Assert.Equal("First", history[0].Content);
        Assert.Equal("Second", history[1].Content);
        Assert.Equal("Third", history[2].Content);
    }

    [Fact]
    public async Task GetHistory_EmptySession_ReturnsEmptyList()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        var history = await _store.GetHistoryAsync(sessionId);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetUserSessions_ReturnsOnlyOwnerSessions()
    {
        await _store.CreateSessionAsync("user1");
        await _store.CreateSessionAsync("user1");
        await _store.CreateSessionAsync("user2");

        var user1Sessions = await _store.GetUserSessionsAsync("user1");
        var user2Sessions = await _store.GetUserSessionsAsync("user2");

        Assert.Equal(2, user1Sessions.Count);
        Assert.Single(user2Sessions);
    }

    [Fact]
    public async Task GetUserSessions_OrderedByUpdatedAtDescending()
    {
        string s1 = await _store.CreateSessionAsync("user1");
        string s2 = await _store.CreateSessionAsync("user1");

        // Append to s1 to update its timestamp
        await _store.AppendMessagesAsync(s1,
        [
            new ChatMessageDto { Role = "user", Content = "Update" },
        ]);

        var sessions = await _store.GetUserSessionsAsync("user1");
        Assert.Equal(s1, sessions[0].Id);
    }

    [Fact]
    public async Task GetSessionOwner_ReturnsCorrectOwner()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        string? owner = await _store.GetSessionOwnerAsync(sessionId);

        Assert.Equal("user1", owner);
    }

    [Fact]
    public async Task GetSessionOwner_NonexistentSession_ReturnsNull()
    {
        string? owner = await _store.GetSessionOwnerAsync("nonexistent");

        Assert.Null(owner);
    }

    [Fact]
    public async Task SessionOwnership_DifferentUserCannotAccessOtherSession()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        string? owner = await _store.GetSessionOwnerAsync(sessionId);
        Assert.NotEqual("user2", owner);
    }

    [Fact]
    public async Task AppendMessages_PreservesAuthorName()
    {
        string sessionId = await _store.CreateSessionAsync("user1");

        await _store.AppendMessagesAsync(sessionId,
        [
            new ChatMessageDto { Role = "user", Content = "Hello", AuthorName = "Alice" },
        ]);

        var history = await _store.GetHistoryAsync(sessionId);
        Assert.Equal("Alice", history[0].AuthorName);
    }
}
