using Discourser.Core.Models;

namespace Discourser.Core.Filters;

public sealed class DeduplicateFilter : IFilter
{
    public IReadOnlyList<Document> Apply(IReadOnlyList<Document> documents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Document>();

        foreach (var doc in documents)
        {
            if (seen.Add(doc.Url))
                result.Add(doc);
        }

        return result;
    }
}
