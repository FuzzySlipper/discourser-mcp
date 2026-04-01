namespace Discourser.Core.Models;

/// <summary>
/// Returned by search tools — list of documents plus query metadata.
/// </summary>
public sealed record SearchResult
{
    public required IReadOnlyList<Document> Documents { get; init; }
    public required string Source { get; init; }
    public required string Query { get; init; }
    public required DateTime RetrievedAt { get; init; }
    public bool FromCache { get; init; }
}
