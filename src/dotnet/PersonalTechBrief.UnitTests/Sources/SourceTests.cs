using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.UnitTests.Sources;

public class SourceTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_trims_name_canonicalizes_url_and_starts_enabled()
    {
        var source = Source.Create("  Engineering feed  ", "HTTPS://Example.test:443/Feed.XML?Case=Keep#top", UtcNow);

        Assert.Equal("Engineering feed", source.Name);
        Assert.Equal("https://example.test/Feed.XML?Case=Keep", source.FeedUrl);
        Assert.Equal("https://example.test/Feed.XML?Case=Keep", source.NormalizedFeedUrl);
        Assert.True(source.IsEnabled);
        Assert.Equal(UtcNow, source.CreatedAtUtc);
        Assert.Equal(UtcNow, source.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("ftp://example.test/feed.xml")]
    [InlineData("https://user:password@example.test/feed.xml")]
    [InlineData("http://localhost/feed.xml")]
    [InlineData("http://localhost./feed.xml")]
    [InlineData("http://127.0.0.1/feed.xml")]
    [InlineData("http://10.0.0.1/feed.xml")]
    [InlineData("http://172.16.0.1/feed.xml")]
    [InlineData("http://192.168.0.1/feed.xml")]
    [InlineData("http://169.254.10.1/feed.xml")]
    [InlineData("http://168.63.129.16/feed.xml")]
    [InlineData("http://[::1]/feed.xml")]
    public void Create_rejects_unsafe_or_unsupported_feed_urls(string feedUrl)
    {
        Assert.Throws<ArgumentException>(() => Source.Create("Feed", feedUrl, UtcNow));
    }

    [Fact]
    public void Create_rejects_a_feed_url_that_cannot_be_indexed_safely()
    {
        var feedUrl = $"https://example.test/{new string('a', SourceFeedUrl.MaxLength)}";

        Assert.Throws<ArgumentException>(() => Source.Create("Feed", feedUrl, UtcNow));
    }

    [Fact]
    public void Create_rejects_a_url_that_expands_past_the_persistence_bound_when_escaped()
    {
        var feedUrl = $"https://example.test/{new string('あ', 100)}";

        Assert.Throws<ArgumentException>(() => Source.Create("Feed", feedUrl, UtcNow));
    }

    [Fact]
    public void Disable_preserves_the_source_and_refreshes_the_timestamp()
    {
        var source = Source.Create("Engineering", "https://example.test/feed.xml", UtcNow);
        var disabledAt = UtcNow.AddMinutes(1);

        source.Disable(disabledAt);

        Assert.False(source.IsEnabled);
        Assert.Equal("Engineering", source.Name);
        Assert.Equal("https://example.test/feed.xml", source.FeedUrl);
        Assert.Equal(UtcNow, source.CreatedAtUtc);
        Assert.Equal(disabledAt, source.UpdatedAtUtc);
    }
}
