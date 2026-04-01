using Discourser.Core.Models;

namespace Discourser.Core.Filters;

public interface IFilter
{
    IReadOnlyList<Document> Apply(IReadOnlyList<Document> documents);
}
