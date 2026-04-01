using Discourser.Core.Models;

namespace Discourser.Core.Data;

public interface ICacheRepository
{
    Task<Document?> GetDocumentAsync(string url, CancellationToken ct = default);
    Task SetDocumentAsync(Document document, TimeSpan ttl, CancellationToken ct = default);

    Task<SearchResult?> GetSearchResultAsync(
        string source, SearchQuery query, CancellationToken ct = default);
    Task SetSearchResultAsync(
        SearchResult result, SearchQuery query, TimeSpan ttl, CancellationToken ct = default);

    Task PurgeExpiredAsync(CancellationToken ct = default);
}
