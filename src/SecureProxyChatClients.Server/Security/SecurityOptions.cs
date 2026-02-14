namespace SecureProxyChatClients.Server.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int MaxMessages { get; set; } = 10;
    public int MaxMessageLength { get; set; } = 4000;
    public int MaxTotalLength { get; set; } = 50000;

    public List<string> AllowedToolNames { get; set; } =
    [
        "GetStoryGraph", "SearchStory", "SaveStoryState", "RollDice", "GetWorldRules"
    ];

    public List<string> BlockedPatterns { get; set; } =
    [
        "ignore previous instructions",
        "ignore all previous",
        "disregard previous",
        "you are now",
        "pretend you are",
        "act as if you are",
        "new instructions:",
        "system prompt:",
        "override instructions",
        "forget your instructions",
        "ignore your instructions"
    ];
}
