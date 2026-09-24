using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.UnitTests.Sources;

public class RssAtomFeedValidatorTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("8.8.8.8");

    [Theory]
    [InlineData("valid-rss.xml")]
    [InlineData("valid-atom.xml")]
    public async Task Validates_supported_fixture_feeds_through_a_public_pinned_address(string fixtureName)
    {
        using var context = CreateContext(_ => CreateXmlResponse(ReadFixture(fixtureName), "application/rss+xml"));

        var valid = await context.Validator.IsSupportedAsync(
            new Uri("https://feeds.example.test/feed.xml"),
            CancellationToken.None);

        Assert.True(valid);
        Assert.Equal(PublicAddress, Assert.Single(context.HttpClientFactory.ValidatedAddresses));
        Assert.Equal("https://feeds.example.test/feed.xml", context.HttpClientFactory.FeedUrls.Single().AbsoluteUri);
        Assert.Equal("PersonalTechBrief/1.0", context.Handler.Request!.Headers.UserAgent.ToString());
        Assert.Contains("application/atom+xml", context.Handler.Request.Headers.Accept.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_mixed_public_and_private_dns_result_without_sending_a_request()
    {
        using var context = CreateContext(
            _ => CreateXmlResponse(ReadFixture("valid-rss.xml"), "application/rss+xml"),
            [PublicAddress, IPAddress.Parse("10.0.0.7")]);

        var valid = await context.Validator.IsSupportedAsync(
            new Uri("https://feeds.example.test/feed.xml"),
            CancellationToken.None);

        Assert.False(valid);
        Assert.Empty(context.HttpClientFactory.ValidatedAddresses);
        Assert.Null(context.Handler.Request);
    }

    [Fact]
    public async Task Rejects_a_trailing_dot_localhost_without_resolving_or_sending_a_request()
    {
        var resolver = new StaticResolver([PublicAddress]);
        using var context = CreateContext(
            _ => CreateXmlResponse(ReadFixture("valid-rss.xml"), "application/rss+xml"),
            resolver: resolver);

        var valid = await context.Validator.IsSupportedAsync(
            new Uri("https://localhost./feed.xml"),
            CancellationToken.None);

        Assert.False(valid);
        Assert.Equal(0, resolver.CallCount);
        Assert.Empty(context.HttpClientFactory.ValidatedAddresses);
    }

    [Fact]
    public async Task Pins_the_connection_to_the_first_validated_address_instead_of_reresolving_for_connection()
    {
        var resolver = new RebindingResolver(PublicAddress, IPAddress.Parse("10.0.0.7"));
        using var context = CreateContext(
            _ => CreateXmlResponse(ReadFixture("valid-rss.xml"), "application/rss+xml"),
            resolver: resolver);

        var valid = await context.Validator.IsSupportedAsync(
            new Uri("https://feeds.example.test/feed.xml"),
            CancellationToken.None);

        Assert.True(valid);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(PublicAddress, Assert.Single(context.HttpClientFactory.ValidatedAddresses));
    }

    [Fact]
    public void Pinned_handler_disables_redirects_and_proxy_and_retains_the_original_sni_host()
    {
        var factory = new PinnedAddressFeedValidationHttpClientFactory(
            Options.Create(new FeedValidationOptions()));
        using var handler = factory.CreateHandler(
            new Uri("https://feeds.example.test/feed.xml"),
            PublicAddress);

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Equal("feeds.example.test", handler.SslOptions.TargetHost);
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public async Task Pinned_handler_connects_to_the_validated_address_without_a_second_dns_lookup()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var feedUri = new Uri($"http://rebinding.example.test:{port}/feed.xml");
        var factory = new PinnedAddressFeedValidationHttpClientFactory(
            Options.Create(new FeedValidationOptions()));
        using var client = new HttpClient(factory.CreateHandler(feedUri, IPAddress.Loopback));

        var responseTask = client.GetStringAsync(feedUri);
        using var acceptedClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await using var stream = acceptedClient.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync();
        string? hostHeader = null;
        string? header;
        while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync()))
        {
            if (header.StartsWith("Host: ", StringComparison.OrdinalIgnoreCase))
            {
                hostHeader = header[6..];
            }
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"));

        Assert.Equal("ok", await responseTask);
        Assert.Equal("GET /feed.xml HTTP/1.1", requestLine);
        Assert.Equal($"rebinding.example.test:{port}", hostHeader);
    }

    [Fact]
    public async Task Rejects_an_xml_document_without_supported_feed_structure()
    {
        using var context = CreateContext(_ => CreateXmlResponse(ReadFixture("unsupported-feed.xml"), "application/xml"));

        var valid = await context.Validator.IsSupportedAsync(new Uri("https://feeds.example.test/invalid.xml"), CancellationToken.None);

        Assert.False(valid);
    }

    [Fact]
    public async Task Rejects_unsafe_xml_entities_without_reading_external_content()
    {
        const string unsafeXml = "<!DOCTYPE rss [<!ENTITY test SYSTEM 'file:///etc/passwd'>]><rss><channel><title>&test;</title></channel></rss>";
        using var context = CreateContext(_ => CreateXmlResponse(unsafeXml, "application/xml"));

        var valid = await context.Validator.IsSupportedAsync(new Uri("https://feeds.example.test/unsafe.xml"), CancellationToken.None);

        Assert.False(valid);
    }

    [Fact]
    public async Task Rejects_a_response_that_exceeds_the_configured_byte_limit()
    {
        using var context = CreateContext(
            _ => CreateXmlResponse(new string('x', 100), "application/xml"),
            maximumResponseBytes: 32);

        var valid = await context.Validator.IsSupportedAsync(new Uri("https://feeds.example.test/large.xml"), CancellationToken.None);

        Assert.False(valid);
    }

    private static ValidationContext CreateContext(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        IReadOnlyList<IPAddress>? addresses = null,
        IFeedHostAddressResolver? resolver = null,
        int maximumResponseBytes = 1_048_576)
    {
        var handler = new CapturingHandler(responseFactory);
        var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTechBrief/1.0");
        var hostResolver = resolver ?? new StaticResolver(addresses ?? [PublicAddress]);
        var clientFactory = new CapturingFeedValidationHttpClientFactory(client);
        var validator = new RssAtomFeedValidator(
            hostResolver,
            clientFactory,
            Options.Create(new FeedValidationOptions { MaximumResponseBytes = maximumResponseBytes, TimeoutSeconds = 15 }),
            NullLogger<RssAtomFeedValidator>.Instance);
        return new ValidationContext(validator, clientFactory, handler, client);
    }

    private static HttpResponseMessage CreateXmlResponse(string xml, string mediaType) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, mediaType),
        };

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sources", "Fixtures", fileName));

    private sealed class ValidationContext(
        RssAtomFeedValidator validator,
        CapturingFeedValidationHttpClientFactory httpClientFactory,
        CapturingHandler handler,
        HttpClient client) : IDisposable
    {
        public RssAtomFeedValidator Validator { get; } = validator;

        public CapturingFeedValidationHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

        public CapturingHandler Handler { get; } = handler;

        public void Dispose() => client.Dispose();
    }

    private sealed class StaticResolver(IReadOnlyList<IPAddress> addresses) : IFeedHostAddressResolver
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(addresses);
        }
    }

    private sealed class RebindingResolver(IPAddress firstAnswer, IPAddress rebindingAnswer) : IFeedHostAddressResolver
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<IPAddress>>(CallCount == 1 ? [firstAnswer] : [rebindingAnswer]);
        }
    }

    private sealed class CapturingFeedValidationHttpClientFactory(HttpClient client) : IFeedValidationHttpClientFactory
    {
        public List<IPAddress> ValidatedAddresses { get; } = [];

        public List<Uri> FeedUrls { get; } = [];

        public HttpClient Create(Uri feedUrl, IPAddress validatedAddress)
        {
            FeedUrls.Add(feedUrl);
            ValidatedAddresses.Add(validatedAddress);
            return client;
        }
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
