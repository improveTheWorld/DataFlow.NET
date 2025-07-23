using System.Threading.Channels;

namespace DataFlow.Framework;

// SelectMany operation implementation
internal class SelectManyParallelAsyncQuery<TSource, TResult> : ParallelAsyncQuery<TResult>
{
    private readonly ParallelAsyncQuery<TSource> _source;
    private readonly Func<TSource, IAsyncEnumerable<TResult>> _selector;

    public SelectManyParallelAsyncQuery(ParallelAsyncQuery<TSource> source, Func<TSource, IAsyncEnumerable<TResult>> selector)
        : base(source.Settings)
    {
        _source = source;
        _selector = selector;
    }

    public override async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var combinedToken = CombineTokens(cancellationToken);

        // SelectMany is inherently difficult to parallelize while preserving outer sequence order
        // without significant buffering. This implementation parallelizes the processing of the
        // outer sequence and then flattens the resulting inner sequences.
        // For unordered execution, this is highly efficient.
        var channel = Channel.CreateBounded<IAsyncEnumerable<TResult>>(_settings.MaxBufferSize);

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _source.Select(item => _selector(item)).WithCancellation(combinedToken))
                {
                    await channel.Writer.WriteAsync(item, combinedToken);
                }
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, combinedToken);

        await foreach (var innerSequence in channel.Reader.ReadAllAsync(combinedToken))
        {
            await foreach (var result in innerSequence.WithCancellation(combinedToken))
            {
                yield return result;
            }
        }

        await producer; // Ensure producer completes and exceptions are propagated.
    }

    public override ParallelAsyncQuery<TResult> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        var newSource = _source.CloneWithNewSettings(settings);
        return new SelectManyParallelAsyncQuery<TSource, TResult>(newSource, _selector);
    }

    private CancellationToken CombineTokens(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default) return cancellationToken;
        if (cancellationToken == default) return _settings.CancellationToken;
        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken).Token;
    }
}
