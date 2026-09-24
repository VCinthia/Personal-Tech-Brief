using System.Net;

namespace PersonalTechBrief.UnitTests.Intelligence;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that records requests (including their
/// serialized bodies) and returns responses produced by a supplied factory. The factory receives
/// the attempt number so tests can model transient failures followed by success.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<int, HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }

    public List<string?> RequestBodies { get; } = [];

    public List<Uri?> RequestUris { get; } = [];

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string json, string mediaType = "application/json") =>
        new((_, _, _, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, mediaType),
        }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var attempt = CallCount;
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        RequestBodies.Add(body);
        RequestUris.Add(request.RequestUri);
        return await responder(attempt, request, body, cancellationToken);
    }
}
