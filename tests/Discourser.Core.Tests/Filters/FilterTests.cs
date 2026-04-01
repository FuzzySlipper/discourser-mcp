using Discourser.Core.Filters;
using Discourser.Core.Models;

namespace Discourser.Core.Tests.Filters;

public class FilterTests
{
    private static Document MakeDoc(string url, int? score = 10, string body = "some words here to fill") =>
        new()
        {
            Url = url,
            Source = "test",
            DocType = "post",
            Title = "Test",
            Body = body,
            Score = score,
            FetchedAt = DateTime.UtcNow
        };

    // --- MinScoreFilter ---

    [Fact]
    public void MinScoreFilter_DropsDocumentsBelowThreshold()
    {
        var docs = new List<Document> { MakeDoc("a", score: 5), MakeDoc("b", score: 15) };
        var result = new MinScoreFilter(10).Apply(docs);
        Assert.Single(result);
        Assert.Equal("b", result[0].Url);
    }

    [Fact]
    public void MinScoreFilter_KeepsDocumentsAtThreshold()
    {
        var docs = new List<Document> { MakeDoc("a", score: 10) };
        var result = new MinScoreFilter(10).Apply(docs);
        Assert.Single(result);
    }

    [Fact]
    public void MinScoreFilter_DropsNullScore()
    {
        var docs = new List<Document> { MakeDoc("a", score: null) };
        var result = new MinScoreFilter(1).Apply(docs);
        Assert.Empty(result);
    }

    // --- MinWordsFilter ---

    [Fact]
    public void MinWordsFilter_DropsShortBodies()
    {
        var docs = new List<Document>
        {
            MakeDoc("a", body: "short"),
            MakeDoc("b", body: "this has enough words to pass the filter easily")
        };
        var result = new MinWordsFilter(5).Apply(docs);
        Assert.Single(result);
        Assert.Equal("b", result[0].Url);
    }

    [Fact]
    public void MinWordsFilter_HandlesEmptyBody()
    {
        var docs = new List<Document> { MakeDoc("a", body: "") };
        var result = new MinWordsFilter(1).Apply(docs);
        Assert.Empty(result);
    }

    [Fact]
    public void MinWordsFilter_KeepsAtThreshold()
    {
        var docs = new List<Document> { MakeDoc("a", body: "one two three") };
        var result = new MinWordsFilter(3).Apply(docs);
        Assert.Single(result);
    }

    // --- DeduplicateFilter ---

    [Fact]
    public void DeduplicateFilter_RemovesDuplicateUrls()
    {
        var docs = new List<Document>
        {
            MakeDoc("a", score: 10),
            MakeDoc("b", score: 20),
            MakeDoc("a", score: 30)
        };
        var result = new DeduplicateFilter().Apply(docs);
        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[0].Score); // Keeps first occurrence
    }

    [Fact]
    public void DeduplicateFilter_PreservesOrderOfUniques()
    {
        var docs = new List<Document> { MakeDoc("c"), MakeDoc("a"), MakeDoc("b") };
        var result = new DeduplicateFilter().Apply(docs);
        Assert.Equal(3, result.Count);
        Assert.Equal("c", result[0].Url);
        Assert.Equal("a", result[1].Url);
        Assert.Equal("b", result[2].Url);
    }

    [Fact]
    public void DeduplicateFilter_CaseInsensitive()
    {
        var docs = new List<Document>
        {
            MakeDoc("https://example.com/A"),
            MakeDoc("https://example.com/a")
        };
        var result = new DeduplicateFilter().Apply(docs);
        Assert.Single(result);
    }
}
