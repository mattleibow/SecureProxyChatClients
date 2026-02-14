using System.ComponentModel;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Tools;

public class GenerateSceneTool
{
    [Description("Generates a new scene for the interactive fiction story based on a prompt and genre")]
    public static SceneResult GenerateScene(
        [Description("The scene prompt or description")] string prompt,
        [Description("The genre (fantasy, sci-fi, mystery, horror)")] string genre,
        [Description("The mood (tense, peaceful, mysterious, epic)")] string mood)
    {
        string id = $"scene-{Guid.NewGuid():N}";
        string title = $"{char.ToUpperInvariant(mood[0])}{mood[1..]} {char.ToUpperInvariant(genre[0])}{genre[1..]} Scene";

        return new SceneResult
        {
            Id = id,
            Title = title,
            Description = $"A {mood} {genre} scene: {prompt}",
            Characters = [$"Protagonist of the {genre} tale"],
            Location = $"A {mood} {genre} setting",
            Mood = mood,
        };
    }
}
