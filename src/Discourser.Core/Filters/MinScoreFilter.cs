using Discourser.Core.Models;

namespace Discourser.Core.Filters;

public sealed class MinScoreFilter(int threshold) : IFilter
{
    public IReadOnlyList<Document> Apply(IReadOnlyList<Document> documents) =>
        documents.Where(d => d.Score.HasValue && d.Score.Value >= threshold).ToList();
}
