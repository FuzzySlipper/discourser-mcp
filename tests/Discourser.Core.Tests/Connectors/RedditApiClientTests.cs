using Discourser.Core.Connectors.Reddit;

namespace Discourser.Core.Tests.Connectors;

public class RedditApiClientTests
{
    [Theory]
    [InlineData(0.5, "hour")]    // 30 minutes
    [InlineData(12, "day")]      // 12 hours
    [InlineData(72, "week")]     // 3 days
    [InlineData(360, "month")]   // 15 days
    [InlineData(4380, "year")]   // ~6 months
    public void MapToTimeWindow_SelectsSmallestContainingWindow(double hoursAgo, string expected)
    {
        var dateFrom = DateTime.UtcNow.AddHours(-hoursAgo);
        var result = RedditApiClient.MapToTimeWindow(dateFrom, null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapToTimeWindow_NullDateFrom_ReturnsNull()
    {
        var result = RedditApiClient.MapToTimeWindow(null, null);
        Assert.Null(result);
    }

    [Fact]
    public void MapToTimeWindow_VeryOldDate_ReturnsNull()
    {
        var dateFrom = DateTime.UtcNow.AddYears(-5);
        var result = RedditApiClient.MapToTimeWindow(dateFrom, null);
        Assert.Null(result); // "all"
    }
}
