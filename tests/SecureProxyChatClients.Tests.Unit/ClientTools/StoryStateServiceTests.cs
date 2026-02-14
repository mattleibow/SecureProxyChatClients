using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.ClientTools;

public class StoryStateServiceTests
{
    private readonly StoryStateService _service = new();

    [Fact]
    public void GetStoryGraph_ReturnsEmptyByDefault()
    {
        StoryGraphResult graph = _service.GetStoryGraph();

        Assert.Empty(graph.Scenes);
        Assert.Empty(graph.Characters);
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void AddScene_AppearsInStoryGraph()
    {
        var scene = new SceneResult
        {
            Id = "s1", Title = "Opening", Description = "The beginning",
            Characters = ["Hero"], Location = "Forest", Mood = "mysterious",
        };

        _service.AddScene(scene);

        Assert.Single(_service.Scenes);
        Assert.Equal("s1", _service.Scenes[0].Id);
    }

    [Fact]
    public void AddCharacter_AppearsInStoryGraph()
    {
        var character = new CharacterResult
        {
            Id = "c1", Name = "Elara", Role = "Protagonist",
            Backstory = "A young mage", Traits = ["brave", "curious"],
        };

        _service.AddCharacter(character);

        Assert.Single(_service.Characters);
        Assert.Equal("Elara", _service.Characters[0].Name);
    }

    [Fact]
    public void AddConnection_AppearsInStoryGraph()
    {
        var connection = new ConnectionDto
        {
            FromSceneId = "s1", ToSceneId = "s2", Label = "goes to",
        };

        _service.AddConnection(connection);

        Assert.Single(_service.Connections);
    }

    [Fact]
    public void AddWorldRule_AppearsInWorldRules()
    {
        _service.AddWorldRule(new WorldRule { Name = "Magic", Description = "Costs life force" });

        WorldRulesResult rules = _service.GetWorldRules();
        Assert.Single(rules.Rules);
        Assert.Equal("Magic", rules.Rules[0].Name);
    }

    [Fact]
    public void GetScene_ReturnsNullForMissingId()
    {
        Assert.Null(_service.GetScene("nonexistent"));
    }

    [Fact]
    public void GetScene_ReturnsScene()
    {
        _service.AddScene(new SceneResult
        {
            Id = "s1", Title = "T", Description = "D",
            Characters = [], Location = "L", Mood = "m",
        });

        Assert.NotNull(_service.GetScene("s1"));
    }

    [Fact]
    public void UpdateSceneContent_ReturnsFalseForMissing()
    {
        Assert.False(_service.UpdateSceneContent("nope", "content"));
    }

    [Fact]
    public void UpdateSceneContent_UpdatesDescription()
    {
        _service.AddScene(new SceneResult
        {
            Id = "s1", Title = "T", Description = "Old",
            Characters = [], Location = "L", Mood = "m",
        });

        bool updated = _service.UpdateSceneContent("s1", "New description");

        Assert.True(updated);
        Assert.Equal("New description", _service.GetScene("s1")!.Description);
    }

    [Fact]
    public void Search_FindsSceneByTitle()
    {
        _service.AddScene(new SceneResult
        {
            Id = "s1", Title = "Dark Forest", Description = "Trees everywhere",
            Characters = [], Location = "Forest", Mood = "mysterious",
        });

        SearchResult result = _service.Search("dark");

        Assert.Single(result.Matches);
        Assert.Equal("scene", result.Matches[0].Type);
    }

    [Fact]
    public void Search_FindsCharacterByName()
    {
        _service.AddCharacter(new CharacterResult
        {
            Id = "c1", Name = "Elara", Role = "Mage",
            Backstory = "Born in the mountains", Traits = ["wise"],
        });

        SearchResult result = _service.Search("elara");

        Assert.Single(result.Matches);
        Assert.Equal("character", result.Matches[0].Type);
    }

    [Fact]
    public void Search_ReturnsEmptyForNoMatch()
    {
        SearchResult result = _service.Search("zzzzz");
        Assert.Empty(result.Matches);
    }
}
