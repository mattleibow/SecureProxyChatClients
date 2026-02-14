namespace SecureProxyChatClients.Shared.Contracts;

public sealed record AnalysisResult
{
    public required IReadOnlyList<string> Themes { get; init; }
    public required IReadOnlyList<string> PlotHoles { get; init; }
    public required IReadOnlyList<string> Suggestions { get; init; }
    public required int TensionLevel { get; init; }
}
