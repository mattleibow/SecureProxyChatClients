using System.ComponentModel;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Tools;

public class CreateCharacterTool
{
    [Description("Creates a new character for the interactive fiction story")]
    public static CharacterResult CreateCharacter(
        [Description("The character's name")] string name,
        [Description("The character's role (protagonist, antagonist, mentor, sidekick)")] string role,
        [Description("The character's backstory")] string backstory,
        [Description("Character personality traits")] IReadOnlyList<string> traits)
    {
        string id = $"char-{Guid.NewGuid():N}";

        return new CharacterResult
        {
            Id = id,
            Name = name,
            Role = role,
            Backstory = backstory,
            Traits = [.. traits],
        };
    }
}
