using Discourser.Core.Models;

namespace Discourser.Core.Connectors;

public interface IConnector
{
    string SourceName { get; }

    Task<SearchResult> SearchAsync(
        SearchQuery query,
        CancellationToken ct = default);

    Task<Document> FetchThreadAsync(
        string url,
        CancellationToken ct = default);
}
