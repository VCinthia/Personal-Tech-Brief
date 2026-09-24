using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Ingestion.Feeds;

internal static class RssAtomFeedParser
{
    private const string AtomNamespace = "http://www.w3.org/2005/Atom";

    public static async Task<IReadOnlyList<RetrievedFeedItem>> ParseAsync(
        Stream responseStream,
        Uri feedUrl,
        long maximumResponseBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var boundedStream = new BoundedReadStream(responseStream, maximumResponseBytes);
            using var reader = XmlReader.Create(boundedStream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = maximumResponseBytes,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            return ParseDocument(document, feedUrl);
        }
        catch (FeedResponseTooLargeException exception)
        {
            throw new FeedRetrievalException(
                "response_too_large",
                "The feed response exceeded the configured size limit.",
                innerException: exception);
        }
        catch (XmlException exception)
        {
            throw new FeedRetrievalException(
                "malformed_feed",
                "The feed XML could not be parsed safely.",
                innerException: exception);
        }
    }

    private static IReadOnlyList<RetrievedFeedItem> ParseDocument(XDocument document, Uri feedUrl)
    {
        var root = document.Root;
        if (root is null)
        {
            throw MalformedFeed();
        }

        if (string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase))
        {
            var channel = root.Elements()
                .SingleOrDefault(element => string.Equals(element.Name.LocalName, "channel", StringComparison.OrdinalIgnoreCase));
            if (channel is null)
            {
                throw MalformedFeed();
            }

            return channel.Elements()
                .Where(element => string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseRssItem(element, feedUrl))
                .Where(item => item is not null)
                .Cast<RetrievedFeedItem>()
                .ToArray();
        }

        if (root.Name == XName.Get("feed", AtomNamespace))
        {
            return root.Elements(XName.Get("entry", AtomNamespace))
                .Select(element => ParseAtomItem(element, feedUrl))
                .Where(item => item is not null)
                .Cast<RetrievedFeedItem>()
                .ToArray();
        }

        throw MalformedFeed();
    }

    private static RetrievedFeedItem? ParseRssItem(XElement item, Uri feedUrl)
    {
        var title = BoundedText(ChildValue(item, "title"), SourceItemText.TitleMaxLength);
        if (title is null)
        {
            return null;
        }

        var externalId = BoundedText(ChildValue(item, "guid"), SourceItemText.ExternalIdMaxLength);
        var url = ToAbsoluteHttpUrl(ChildValue(item, "link"), feedUrl);
        var excerpt = BoundedText(
            ChildValue(item, "description") ?? ChildValue(item, "encoded"),
            SourceItemText.ExcerptMaxLength);
        var publishedAtUtc = ParseUtc(ChildValue(item, "pubDate"));
        return new RetrievedFeedItem(
            externalId,
            url,
            title,
            excerpt,
            publishedAtUtc,
            CreateContentHash(title, excerpt));
    }

    private static RetrievedFeedItem? ParseAtomItem(XElement entry, Uri feedUrl)
    {
        var title = BoundedText(ChildValue(entry, "title"), SourceItemText.TitleMaxLength);
        if (title is null)
        {
            return null;
        }

        var link = entry.Elements(XName.Get("link", AtomNamespace))
            .OrderByDescending(element => string.Equals((string?)element.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("href"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var externalId = BoundedText(ChildValue(entry, "id"), SourceItemText.ExternalIdMaxLength);
        var url = ToAbsoluteHttpUrl(link, feedUrl);
        var excerpt = BoundedText(
            ChildValue(entry, "summary") ?? ChildValue(entry, "content"),
            SourceItemText.ExcerptMaxLength);
        var publishedAtUtc = ParseUtc(ChildValue(entry, "published") ?? ChildValue(entry, "updated"));
        return new RetrievedFeedItem(
            externalId,
            url,
            title,
            excerpt,
            publishedAtUtc,
            CreateContentHash(title, excerpt));
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return null;
        }

        return parsed.UtcDateTime;
    }

    private static string? ToAbsoluteHttpUrl(string? value, Uri feedUrl)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(feedUrl, value.Trim(), out var candidate) ||
            (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            _ = SourceItemUrl.Prepare(candidate.AbsoluteUri);
            return candidate.AbsoluteUri;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? BoundedText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Feed descriptions frequently contain HTML inside CDATA. Store a bounded text excerpt,
        // not markup that a later UI or model boundary could mistake for executable/prompt content.
        var plainText = StripMarkup(System.Net.WebUtility.HtmlDecode(value));
        var collapsed = string.Join(' ', plainText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= maximumLength ? collapsed : collapsed[..maximumLength];
    }

    private static string StripMarkup(string value)
    {
        var builder = new StringBuilder(value.Length);
        var insideTag = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '<' && LooksLikeMarkup(value, index))
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool LooksLikeMarkup(string value, int index)
    {
        if (index + 1 >= value.Length)
        {
            return false;
        }

        var next = value[index + 1];
        if (next is '!' or '?')
        {
            return true;
        }

        if (next == '/')
        {
            return index + 2 < value.Length && char.IsLetter(value[index + 2]);
        }

        return char.IsLetter(next);
    }

    private static string? CreateContentHash(string title, string? excerpt)
    {
        // Title-only entries use the bounded title window, not a permanent content hash.
        if (string.IsNullOrWhiteSpace(excerpt))
        {
            return null;
        }

        var value = string.Concat(title, "\n", excerpt);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static FeedRetrievalException MalformedFeed() =>
        new("malformed_feed", "The response is not a supported RSS or Atom feed.");

    private sealed class FeedResponseTooLargeException : Exception;

    private sealed class BoundedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long bytesRead;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(Limit(buffer));
            Track(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(Limit(buffer), cancellationToken);
            Track(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private Span<byte> Limit(Span<byte> buffer)
        {
            var remainingPlusOne = checked(maximumBytes - bytesRead + 1);
            return buffer[..(int)Math.Min(buffer.Length, remainingPlusOne)];
        }

        private Memory<byte> Limit(Memory<byte> buffer)
        {
            var remainingPlusOne = checked(maximumBytes - bytesRead + 1);
            return buffer[..(int)Math.Min(buffer.Length, remainingPlusOne)];
        }

        private void Track(int read)
        {
            bytesRead += read;
            if (bytesRead > maximumBytes)
            {
                throw new FeedResponseTooLargeException();
            }
        }
    }
}
