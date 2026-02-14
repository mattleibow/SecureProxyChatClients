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

    /// <summary>
    /// Builds a scoped context string for a given scene including its immediate neighbors.
    /// Limited to <paramref name="maxChars"/> to prevent token overflow.
    /// </summary>
    public string BuildScopedContext(string sceneId, int maxChars = 2000)
    {
        var scene = GetScene(sceneId);
        if (scene is null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Current Scene: {scene.Title}");
        sb.AppendLine($"Location: {scene.Location} | Mood: {scene.Mood}");
        sb.AppendLine(scene.Description);

        // Characters in scene
        foreach (var charId in scene.Characters)
        {
            var character = _characters.FirstOrDefault(c => c.Id == charId || c.Name == charId);
            if (character is not null && sb.Length < maxChars)
                sb.AppendLine($"Character: {character.Name} ({character.Role}) — {Truncate(character.Backstory, 100)}");
        }

        // Immediate neighbor scenes via connections
        var neighborIds = _connections
            .Where(c => c.FromSceneId == sceneId || c.ToSceneId == sceneId)
            .Select(c => c.FromSceneId == sceneId ? c.ToSceneId : c.FromSceneId)
            .Distinct();

        foreach (var neighborId in neighborIds)
        {
            if (sb.Length >= maxChars) break;
            var neighbor = GetScene(neighborId);
            if (neighbor is not null)
                sb.AppendLine($"Nearby: {neighbor.Title} — {Truncate(neighbor.Description, 100)}");
        }

        // World rules summary
        if (_worldRules.Count > 0 && sb.Length < maxChars)
        {
            sb.AppendLine("World Rules:");
            foreach (var rule in _worldRules)
            {
                if (sb.Length >= maxChars) break;
                sb.AppendLine($"- {rule.Name}: {Truncate(rule.Description, 80)}");
            }
        }

        return sb.Length > maxChars ? sb.ToString()[..maxChars] : sb.ToString();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
