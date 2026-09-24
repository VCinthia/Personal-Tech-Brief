using PersonalTechBrief.Infrastructure.Articles;

namespace PersonalTechBrief.UnitTests.Articles;

public sealed class ArticleTextExtractorTests
{
    private readonly ArticleTextExtractor extractor = new();

    [Fact]
    public void Prefers_the_open_graph_title()
    {
        const string html = """
            <html><head>
              <meta property="og:title" content="Open Graph Title" />
              <title>Fallback Title</title>
            </head><body><article><p>Body text here.</p></article></body></html>
            """;

        var result = extractor.Extract(html);

        Assert.NotNull(result);
        Assert.Equal("Open Graph Title", result!.Title);
    }

    [Fact]
    public void Falls_back_to_the_document_title_then_the_first_heading()
    {
        Assert.Equal("Doc Title", extractor.Extract(
            "<html><head><title>Doc Title</title></head><body><article><p>Text.</p></article></body></html>")?.Title);
        Assert.Equal("Heading", extractor.Extract(
            "<html><body><article><h1>Heading</h1><p>Text.</p></article></body></html>")?.Title);
    }

    [Fact]
    public void Extracts_the_article_body_and_ignores_boilerplate()
    {
        const string html = """
            <html><head><title>T</title></head><body>
              <nav>Home About Contact</nav>
              <header>Site header</header>
              <article><p>The real article content.</p><p>Second paragraph.</p></article>
              <aside>Related links</aside>
              <footer>Copyright</footer>
              <script>trackUser();</script>
              <style>.a{color:red}</style>
            </body></html>
            """;

        var result = extractor.Extract(html);

        Assert.NotNull(result);
        Assert.Contains("The real article content.", result!.Text);
        Assert.Contains("Second paragraph.", result.Text);
        Assert.DoesNotContain("Home About Contact", result.Text);
        Assert.DoesNotContain("Site header", result.Text);
        Assert.DoesNotContain("Related links", result.Text);
        Assert.DoesNotContain("Copyright", result.Text);
        Assert.DoesNotContain("trackUser", result.Text);
        Assert.DoesNotContain("color:red", result.Text);
    }

    [Fact]
    public void Selects_the_densest_paragraph_container_without_a_semantic_wrapper()
    {
        const string html = """
            <html><head><title>T</title></head><body>
              <div class="sidebar"><p>Ad.</p></div>
              <div class="content"><p>This container holds the substantial article body that a reader wants.</p>
              <p>It has clearly more text than the sidebar block next to it.</p></div>
            </body></html>
            """;

        var result = extractor.Extract(html);

        Assert.NotNull(result);
        Assert.Contains("substantial article body", result!.Text);
        Assert.DoesNotContain("Ad.", result.Text);
    }

    [Fact]
    public void Collapses_whitespace()
    {
        var result = extractor.Extract(
            "<html><head><title>T</title></head><body><article><p>one\n\n   two\t\tthree</p></article></body></html>");

        Assert.NotNull(result);
        Assert.Equal("one two three", result!.Text);
    }

    [Fact]
    public void Returns_null_when_no_title_is_present()
    {
        Assert.Null(extractor.Extract("<html><body><article><p>Text without any title.</p></article></body></html>"));
    }

    [Fact]
    public void Returns_null_when_no_body_text_survives()
    {
        Assert.Null(extractor.Extract("<html><head><title>Only a title</title></head><body><nav>menu</nav></body></html>"));
    }

    [Fact]
    public void Returns_null_for_empty_input()
    {
        Assert.Null(extractor.Extract(string.Empty));
        Assert.Null(extractor.Extract("   "));
    }
}
