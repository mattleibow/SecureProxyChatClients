using System.ComponentModel;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Tools;

public class SuggestTwistTool
{
    [Description("Suggests a plot twist based on the current story state and tension level")]
    public static TwistResult SuggestTwist(
        [Description("A summary of the current story context")] string storyContext,
        [Description("Current tension level from 1 (calm) to 10 (climax)")] int currentTension)
    {
        int clampedTension = Math.Clamp(currentTension, 1, 10);

        string description = clampedTension switch
        {
            <= 3 => "A mysterious stranger arrives with knowledge that challenges the protagonist's beliefs",
            <= 6 => "An ally reveals a hidden agenda that shifts the power dynamic",
            <= 8 => "The true antagonist is unmasked — it was someone the protagonist trusted all along",
            _ => "The world itself changes: what was believed to be real is revealed as an illusion",
        };

        int impactLevel = Math.Clamp(clampedTension + 2, 1, 10);

        List<string> affectedCharacters = ["protagonist"];
        if (clampedTension > 5)
            affectedCharacters.Add("antagonist");
        if (clampedTension > 7)
            affectedCharacters.Add("mentor");

        return new TwistResult
        {
            Description = description,
            ImpactLevel = impactLevel,
            AffectedCharacters = affectedCharacters,
        };
    }
}
