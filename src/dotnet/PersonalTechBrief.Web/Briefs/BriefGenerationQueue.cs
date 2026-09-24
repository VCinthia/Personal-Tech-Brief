using System.Threading.Channels;

namespace PersonalTechBrief.Web.Briefs;

/// <summary>In-process hand-off from the <c>POST /briefs</c> request to the background generator.</summary>
public interface IBriefGenerationQueue
{
    /// <summary>Enqueues a created brief for asynchronous generation.</summary>
    ValueTask EnqueueAsync(Guid briefId, CancellationToken cancellationToken);

    /// <summary>Yields queued brief ids until the token is cancelled (single consumer).</summary>
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Unbounded single-consumer channel backing the async 202 flow. Generation is serialized so each new
/// brief's window is computed from the last completed brief (§20) without concurrent runs racing on the
/// same candidate set.
/// </summary>
public sealed class BriefGenerationQueue : IBriefGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(Guid briefId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(briefId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
