using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Services;

/// <summary>
/// Manages local story data in-memory. Future phases will add IndexedDB persistence.
/// </summary>
public sealed class StoryStateService
{
    private readonly List<SceneResult> _scenes = [];
    private readonly List<CharacterResult> _characters = [];
    private readonly List<ConnectionDto> _connections = [];
    private readonly List<WorldRule> _worldRules = [];

    public IReadOnlyList<SceneResult> Scenes => _scenes;
    public IReadOnlyList<CharacterResult> Characters => _characters;
    public IReadOnlyList<ConnectionDto> Connections => _connections;
    public IReadOnlyList<WorldRule> WorldRules => _worldRules;

    public StoryGraphResult GetStoryGraph() => new()
    {
        Scenes = _scenes.ToList(),
        Characters = _characters.ToList(),
        Connections = _connections.ToList(),
    };

    public void AddScene(SceneResult scene) => _scenes.Add(scene);

    public void AddCharacter(CharacterResult character) => _characters.Add(character);

    public void AddConnection(ConnectionDto connection) => _connections.Add(connection);

    public void AddWorldRule(WorldRule rule) => _worldRules.Add(rule);

    public SceneResult? GetScene(string sceneId) =>
        _scenes.FirstOrDefault(s => s.Id == sceneId);

    public bool UpdateSceneContent(string sceneId, string content)
    {
        int index = _scenes.FindIndex(s => s.Id == sceneId);
        if (index < 0) return false;

        _scenes[index] = _scenes[index] with { Description = content };
        return true;
    }

    public SearchResult Search(string query)
    {
        var q = query.ToUpperInvariant();
        var matches = new List<SearchMatch>();

        foreach (var scene in _scenes)
        {
            if (scene.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                scene.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new SearchMatch
                {
                    Type = "scene",
                    Id = scene.Id,
                    Title = scene.Title,
                    Snippet = Truncate(scene.Description, 200),
                });
            }
        }

        foreach (var character in _characters)
        {
            if (character.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                character.Backstory.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new SearchMatch
                {
                    Type = "character",
                    Id = character.Id,
                    Title = character.Name,
                    Snippet = Truncate(character.Backstory, 200),
                });
            }
        }

        return new SearchResult { Matches = matches };
    }

    public WorldRulesResult GetWorldRules() => new() { Rules = _worldRules.ToList() };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
