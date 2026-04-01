using System.Text.Json;
using System.Text.Json.Nodes;
using Discourser.Core.Data;
using Discourser.Core.Models;

namespace Discourser.Core.Tests.Data;

public class CacheRepositoryTests : IAsyncLifetime
{
    private readonly TestDb _testDb = new();
    private SqliteCacheRepository _repo = null!;

    public async Task InitializeAsync()
    {
        await _testDb.InitializeAsync();
        _repo = new SqliteCacheRepository(_testDb.Db);
    }

    public Task DisposeAsync() => _testDb.DisposeAsync();

    private static Document MakeDoc(string url = "https://reddit.com/r/test/comments/abc/test_post/") =>
        new()
        {
            Url = url,
            Source = "reddit",
            DocType = "thread",
            Title = "Test Post",
            Body = "This is the body",
            Score = 42,
            FetchedAt = DateTime.UtcNow,
            PublishedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            ParentUrl = null,
            Metadata = JsonSerializer.Deserialize<JsonElement>(
                new JsonObject { ["subreddit"] = "test", ["author"] = "testuser" }.ToJsonString())
        };

    [Fact]
    public async Task StoreAndRetrieveDocument_RoundTrips()
    {
        var doc = MakeDoc();
        await _repo.SetDocumentAsync(doc, TimeSpan.FromHours(1));

        var retrieved = await _repo.GetDocumentAsync(doc.Url);

        Assert.NotNull(retrieved);
        Assert.Equal(doc.Url, retrieved.Url);
        Assert.Equal(doc.Title, retrieved.Title);
        Assert.Equal(doc.Body, retrieved.Body);
        Assert.Equal(doc.Score, retrieved.Score);
        Assert.Equal(doc.ParentUrl, retrieved.ParentUrl);
        Assert.Equal(doc.Source, retrieved.Source);
        Assert.Equal(doc.DocType, retrieved.DocType);
    }

    [Fact]
    public async Task StoreAndRetrieveDocument_MetadataRoundTrips()
    {
        var doc = MakeDoc();
        await _repo.SetDocumentAsync(doc, TimeSpan.FromHours(1));

        var retrieved = await _repo.GetDocumentAsync(doc.Url);

        Assert.NotNull(retrieved);
        Assert.True(retrieved.Metadata.HasValue);
        Assert.Equal("test",
            retrieved.Metadata.Value.GetProperty("subreddit").GetString());
        Assert.Equal("testuser",
            retrieved.Metadata.Value.GetProperty("author").GetString());
    }

    [Fact]
    public async Task ExpiredDocument_ReturnsNull()
    {
        var doc = MakeDoc();
        await _repo.SetDocumentAsync(doc, TimeSpan.FromMilliseconds(-1));

        var retrieved = await _repo.GetDocumentAsync(doc.Url);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task CacheMiss_ReturnsNull()
    {
        var result = await _repo.GetDocumentAsync("https://nonexistent.com/thread");
        Assert.Null(result);
    }

    [Fact]
    public async Task OverwriteDocument_ReplacesExisting()
    {
        var doc1 = MakeDoc();
        await _repo.SetDocumentAsync(doc1, TimeSpan.FromHours(1));

        var doc2 = doc1 with { Title = "Updated Title", Score = 99 };
        await _repo.SetDocumentAsync(doc2, TimeSpan.FromHours(1));

        var retrieved = await _repo.GetDocumentAsync(doc1.Url);
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Title", retrieved.Title);
        Assert.Equal(99, retrieved.Score);
    }

    [Fact]
    public async Task StoreAndRetrieveSearchResult_RoundTrips()
    {
        var query = new SearchQuery { Text = "test query", MinScore = 5 };
        var result = new SearchResult
        {
            Documents = new List<Document> { MakeDoc() },
            Source = "reddit",
            Query = "test query",
            RetrievedAt = DateTime.UtcNow,
            FromCache = false
        };

        await _repo.SetSearchResultAsync(result, query, TimeSpan.FromHours(1));

        var retrieved = await _repo.GetSearchResultAsync("reddit", query);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Documents);
        Assert.Equal("test query", retrieved.Query);
    }

    [Fact]
    public async Task ExpiredSearchResult_ReturnsNull()
    {
        var query = new SearchQuery { Text = "expired query" };
        var result = new SearchResult
        {
            Documents = new List<Document>(),
            Source = "reddit",
            Query = "expired query",
            RetrievedAt = DateTime.UtcNow,
            FromCache = false
        };

        await _repo.SetSearchResultAsync(result, query, TimeSpan.FromMilliseconds(-1));

        var retrieved = await _repo.GetSearchResultAsync("reddit", query);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task PurgeExpired_RemovesExpiredRows()
    {
        var doc = MakeDoc();
        await _repo.SetDocumentAsync(doc, TimeSpan.FromMilliseconds(-1));

        var freshDoc = MakeDoc("https://reddit.com/r/test/comments/def/fresh/");
        await _repo.SetDocumentAsync(freshDoc, TimeSpan.FromHours(1));

        await _repo.PurgeExpiredAsync();

        Assert.Null(await _repo.GetDocumentAsync(doc.Url));
        Assert.NotNull(await _repo.GetDocumentAsync(freshDoc.Url));
    }
}
