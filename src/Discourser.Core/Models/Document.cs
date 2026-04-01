using System.Text.Json;

namespace Discourser.Core.Models;

/// <summary>
/// A single piece of content — post, thread, or comment.
/// </summary>
public sealed record Document
{
    public required string Url { get; init; }
    public required string Source { get; init; }
    public required string DocType { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public int? Score { get; init; }
    public required DateTime FetchedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? ParentUrl { get; init; }
    public JsonElement? Metadata { get; init; }
}
