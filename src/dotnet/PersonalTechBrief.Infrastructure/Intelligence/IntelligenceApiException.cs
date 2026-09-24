using System.Net;

namespace PersonalTechBrief.Infrastructure.Intelligence;

/// <summary>
/// Raised when the Intelligence API returns a non-success response or the request cannot be
/// completed (transport failure or client-side timeout). It carries only the HTTP status and the
/// stable problem+json <c>title</c>; it never surfaces provider, credential, prompt, or raw content
/// detail, so internal specifics are not leaked to callers or logs.
/// </summary>
public sealed class IntelligenceApiException : Exception
{
    public IntelligenceApiException(
        string message,
        HttpStatusCode? statusCode = null,
        string? problemTitle = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ProblemTitle = problemTitle;
    }

    /// <summary>HTTP status returned by the service, or null when no response was received (timeout/transport).</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Stable problem+json title, when the error body was a well-formed problem document.</summary>
    public string? ProblemTitle { get; }
}
