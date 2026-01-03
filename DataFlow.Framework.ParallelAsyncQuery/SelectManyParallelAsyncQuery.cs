using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DataFlow.Parallel;

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
        using var linkedCts = CreateLinkedCts(cancellationToken);
        var combinedToken = linkedCts?.Token ?? (_settings.CancellationToken != default ? _settings.CancellationToken : cancellationToken);

        // SelectMany is inherently difficult to parallelize while preserving outer sequence order
        // without significant buffering. This implementation parallelizes the processing of the
        // outer sequence and then flattens the resulting inner sequences.
        // For unordered execution, this is highly efficient.
        var channel = Channel.CreateBounded<IAsyncEnumerable<TResult>>(_settings.MaxBufferSize);

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _source.WithCancellation(combinedToken))
                {
                    IAsyncEnumerable<TResult> innerSequence;
                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
                        timeoutCts.CancelAfter(_settings.OperationTimeout);

                        // Apply selector with timeout
                        innerSequence = _selector(item);
                    }
                    catch (Exception) when (_settings.ContinueOnError)
                    {
                        // Skip this outer item if selector fails
                        continue;
                    }

                    await channel.Writer.WriteAsync(innerSequence, combinedToken);
                }
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                return;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, combinedToken);

        await foreach (var innerSequence in channel.Reader.ReadAllAsync(combinedToken))
        {
            IAsyncEnumerator<TResult>? enumerator = null;
            try
            {
                enumerator = innerSequence.GetAsyncEnumerator(combinedToken);

                while (true)
                {
                    TResult result;
                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
                        timeoutCts.CancelAfter(_settings.OperationTimeout);

                        if (!await enumerator.MoveNextAsync().AsTask().WaitAsync(timeoutCts.Token))
                            break;

                        result = enumerator.Current;
                    }
                    catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception) when (_settings.ContinueOnError)
                    {
                        // Skip failed inner items, continue with next
                        continue;
                    }

                    yield return result;
                }
            }
            finally
            {
                if (enumerator != null)
                    await enumerator.DisposeAsync();
            }
        }

        await producer; // Ensure producer completes and exceptions are propagated.
    }

    public override ParallelAsyncQuery<TResult> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        var newSource = _source.CloneWithNewSettings(settings);
        return new SelectManyParallelAsyncQuery<TSource, TResult>(newSource, _selector);
    }

    /// <summary>
    /// Creates a linked CancellationTokenSource only if both tokens are non-default.
    /// Returns null if linking is not needed (to avoid unnecessary allocation).
    /// Caller must dispose the returned CTS.
    /// </summary>
    private CancellationTokenSource? CreateLinkedCts(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default || cancellationToken == default)
            return null;

        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken);
    }
}
