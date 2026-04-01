namespace Discourser.Core.Models;

/// <summary>
/// The user's search intent — passed to connectors.
/// </summary>
public sealed record SearchQuery
{
    public required string Text { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int? MinScore { get; init; }
    public int? MinWords { get; init; }
    public int? MaxResults { get; init; }
    public Dictionary<string, string> SiteFilters { get; init; } = new();
}
