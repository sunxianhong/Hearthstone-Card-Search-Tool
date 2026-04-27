namespace HearthstoneCardSearchTool.Core;

public sealed class CardDataMapOverrideConfig
{
    public Dictionary<string, string> UnknownEnumMap { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> TagLabels { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ClassMap { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> RarityMap { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> RaceMap { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SchoolMap { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SetMap { get; init; } = new(StringComparer.Ordinal);
}
