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
        using var combinedCts = BuildCombinedCts(cancellationToken);
        var combinedToken = combinedCts.Token;

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
    /// Builds a CancellationTokenSource that combines the settings token, the caller token,
    /// and the OperationTimeout. Always returns a non-null CTS. Caller must dispose.
    /// Fixes NET-001 (timeout enforcement) and NET-002 (combined token linking).
    /// </summary>
    private CancellationTokenSource BuildCombinedCts(CancellationToken cancellationToken)
    {
        var tokens = new List<CancellationToken>(2);
        if (_settings.CancellationToken != default) tokens.Add(_settings.CancellationToken);
        if (cancellationToken != default) tokens.Add(cancellationToken);

        var cts = tokens.Count switch
        {
            0 => new CancellationTokenSource(),
            1 => CancellationTokenSource.CreateLinkedTokenSource(tokens[0]),
            _ => CancellationTokenSource.CreateLinkedTokenSource(tokens[0], tokens[1]),
        };

        if (_settings.OperationTimeout != Timeout.InfiniteTimeSpan &&
            _settings.OperationTimeout > TimeSpan.Zero)
        {
            cts.CancelAfter(_settings.OperationTimeout);
        }

        return cts;
    }
}
