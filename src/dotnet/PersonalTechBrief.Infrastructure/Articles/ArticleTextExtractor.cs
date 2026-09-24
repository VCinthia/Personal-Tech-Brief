using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace PersonalTechBrief.Infrastructure.Articles;

public sealed record ArticleExtraction(string Title, string Text);

/// <summary>
/// Extracts a title plus readable main text from fetched HTML. Implementations must never execute
/// scripts or render markup — parse-only (see D-024).
/// </summary>
public interface IArticleTextExtractor
{
    ArticleExtraction? Extract(string html);
}

/// <summary>
/// Parse-only, readability-lite extraction over the AngleSharp DOM. It drops non-content and
/// boilerplate regions, prefers a semantic article/main container (falling back to the densest
/// paragraph container, then the body), and collapses whitespace. Returns null when no usable
/// title or text survives.
/// </summary>
public sealed class ArticleTextExtractor : IArticleTextExtractor
{
    private static readonly string[] BoilerplateSelectors =
        ["script", "style", "noscript", "template", "head", "nav", "header", "footer", "aside", "form"];

    private readonly HtmlParser parser = new();

    public ArticleExtraction? Extract(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        using var document = parser.ParseDocument(html);

        var title = ExtractTitle(document);

        foreach (var element in document.QuerySelectorAll(string.Join(',', BoilerplateSelectors)).ToArray())
        {
            element.Remove();
        }

        var text = Normalize(SelectContentRoot(document)?.TextContent);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new ArticleExtraction(title, text);
    }

    private static string? ExtractTitle(IHtmlDocument document)
    {
        var ogTitle = document
            .QuerySelectorAll("meta")
            .FirstOrDefault(meta =>
                string.Equals(meta.GetAttribute("property"), "og:title", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(meta.GetAttribute("name"), "og:title", StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("content");
        if (!string.IsNullOrWhiteSpace(ogTitle))
        {
            return Normalize(ogTitle);
        }

        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            return Normalize(document.Title);
        }

        return Normalize(document.QuerySelector("h1")?.TextContent);
    }

    private static IElement? SelectContentRoot(IHtmlDocument document)
    {
        var semantic = document.QuerySelector("article") ?? document.QuerySelector("main");
        if (semantic is not null)
        {
            return semantic;
        }

        IElement? densest = null;
        var densestLength = 0;
        foreach (var candidate in document.QuerySelectorAll("div,section"))
        {
            var length = candidate.QuerySelectorAll("p").Sum(paragraph => paragraph.TextContent?.Length ?? 0);
            if (length > densestLength)
            {
                densestLength = length;
                densest = candidate;
            }
        }

        return densest ?? document.Body;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
