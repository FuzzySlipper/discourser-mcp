using Discourser.Core.Models;

namespace Discourser.Core.Filters;

public sealed class MinWordsFilter(int threshold) : IFilter
{
    public IReadOnlyList<Document> Apply(IReadOnlyList<Document> documents) =>
        documents.Where(d => CountWords(d.Body) >= threshold).ToList();

    private static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
