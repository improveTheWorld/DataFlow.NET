using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// Configuration settings
public record ParallelExecutionSettings
{
    private int _maxConcurrency = Environment.ProcessorCount;
    private int _maxBufferSize = 1000;

    public int MaxConcurrency
    {
        get => _maxConcurrency;
        init => _maxConcurrency = Math.Max(1, Math.Min(value, 100));
    }

    public int MaxBufferSize
    {
        get => _maxBufferSize;
        init => _maxBufferSize = Math.Max(10, Math.Min(value, 10000));
    }

    public CancellationToken CancellationToken { get; init; } = default;
    public ParallelExecutionMode ExecutionMode { get; init; } = ParallelExecutionMode.Default;
    public ParallelMergeOptions MergeOptions { get; init; } = ParallelMergeOptions.AutoBuffered;
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool PreserveOrder { get; init; } = true;
    public bool ContinueOnError { get; init; } = false;
}

public enum ParallelExecutionMode
{
    Default,
    ForceParallel,
    Sequential
}

public enum ParallelMergeOptions
{
    Default,
    NotBuffered,
    AutoBuffered,
    FullyBuffered
}

// Base parallel async query class
public abstract class ParallelAsyncQuery<TSource> : IAsyncEnumerable<TSource>
{
    protected readonly ParallelExecutionSettings _settings;

    protected ParallelAsyncQuery(ParallelExecutionSettings settings)
    {
        _settings = settings ?? new ParallelExecutionSettings();
    }

    public ParallelExecutionSettings Settings => _settings;

    public abstract IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default);

    // Make this method public so derived classes can call it on other instances
    public abstract ParallelAsyncQuery<TSource> CloneWithNewSettings(ParallelExecutionSettings settings);

    public ParallelAsyncQuery<TSource> AsSequential()
    {
        var sequentialSettings = _settings with { ExecutionMode = ParallelExecutionMode.Sequential };
        return CloneWithNewSettings(sequentialSettings);
    }

    public ParallelAsyncQuery<TSource> WithMaxConcurrency(int maxConcurrency)
    {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        var newSettings = _settings with { MaxConcurrency = maxConcurrency };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> WithCancellation(CancellationToken cancellationToken)
    {
        var newSettings = _settings with { CancellationToken = cancellationToken };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> WithTimeout(TimeSpan timeout)
    {
        var newSettings = _settings with { OperationTimeout = timeout };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> WithOrderPreservation(bool preserveOrder = true)
    {
        var newSettings = _settings with { PreserveOrder = preserveOrder };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> WithBufferSize(int bufferSize)
    {
        var newSettings = _settings with { MaxBufferSize = bufferSize };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> WithMergeOptions(ParallelMergeOptions mergeOptions)
    {
        var newSettings = _settings with { MergeOptions = mergeOptions };
        return CloneWithNewSettings(newSettings);
    }

    public ParallelAsyncQuery<TSource> ContinueOnError(bool continueOnError = true)
    {
        var newSettings = _settings with { ContinueOnError = continueOnError };
        return CloneWithNewSettings(newSettings);
    }
}

// Source wrapper implementation
internal class SourceParallelAsyncQuery<TSource> : ParallelAsyncQuery<TSource>
{
    private readonly IAsyncEnumerable<TSource> _source;

    public SourceParallelAsyncQuery(IAsyncEnumerable<TSource> source, ParallelExecutionSettings settings)
        : base(settings)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public override async IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var combinedToken = CombineTokens(cancellationToken);

        if (_settings.ExecutionMode == ParallelExecutionMode.Sequential)
        {
            await foreach (var item in _source.WithCancellation(combinedToken))
            {
                yield return item;
            }
            yield break;
        }

        using var semaphore = new SemaphoreSlim(_settings.MaxConcurrency, _settings.MaxConcurrency);

        await foreach (var item in _source.WithCancellation(combinedToken))
        {
            await semaphore.WaitAsync(combinedToken);
            try
            {
                yield return item;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public override ParallelAsyncQuery<TSource> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        return new SourceParallelAsyncQuery<TSource>(_source, settings);
    }

    private CancellationToken CombineTokens(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default)
            return cancellationToken;
        if (cancellationToken == default)
            return _settings.CancellationToken;

        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken).Token;
    }
}

// Select operation implementation
internal class SelectParallelAsyncQuery<TSource, TResult> : ParallelAsyncQuery<TResult>
{
    private readonly ParallelAsyncQuery<TSource> _source;
    private readonly Func<TSource, Task<TResult>> _selector;
    private readonly Func<TSource, int, Task<TResult>> _indexedSelector;
    private readonly bool _useIndex;

    public SelectParallelAsyncQuery(ParallelAsyncQuery<TSource> source, Func<TSource, Task<TResult>> selector)
        : base(source.Settings)
    {
        _source = source;
        _selector = selector;
    }
    public SelectParallelAsyncQuery(ParallelAsyncQuery<TSource> source, Func<TSource, int, Task<TResult>> selector)
       : base(source.Settings)
    {
        _source = source;
        _indexedSelector = selector;
        _useIndex = true;
    }
    public override async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var combinedToken = CombineTokens(cancellationToken);

        if (_settings.ExecutionMode == ParallelExecutionMode.Sequential)
        {
            var index = 0;
            await foreach (var item in _source.WithCancellation(combinedToken))
            {
                
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
                timeoutCts.CancelAfter(_settings.OperationTimeout);

                var result = _useIndex
                    ? await _indexedSelector(item, index++).WaitAsync(timeoutCts.Token)
                    : await _selector(item).WaitAsync(timeoutCts.Token);
                yield return result;                
            }
            yield break;
        }

        if (_settings.PreserveOrder)
        {
            await foreach (var result in GetOrderedResults(combinedToken))
            {
                yield return result;
            }
        }
        else
        {
            await foreach (var result in GetUnorderedResults(combinedToken))
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<TResult> GetOrderedResults([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = new List<(TSource Item, int Index)>();
        var index = 0;

        await foreach (var item in _source.WithCancellation(cancellationToken))
        {
            items.Add((item, index++));
        }

        using var semaphore = new SemaphoreSlim(_settings.MaxConcurrency, _settings.MaxConcurrency);
        var tasks = items.Select(async itemWithIndex =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_settings.OperationTimeout);

                var result = _useIndex
                    ? await _indexedSelector(itemWithIndex.Item, itemWithIndex.Index).WaitAsync(timeoutCts.Token)
                    : await _selector(itemWithIndex.Item).WaitAsync(timeoutCts.Token);
                return (Result: result, Index: itemWithIndex.Index, Success: true);
            }
            catch (Exception) when (_settings.ContinueOnError)
            {
                return (Result: default(TResult), Index: itemWithIndex.Index, Success: false);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results.OrderBy(r => r.Index))
        {
            if (result.Success)
            {
                yield return result.Result;
            }
        }
    }

    private async IAsyncEnumerable<TResult> GetUnorderedResults([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<TResult>(new BoundedChannelOptions(_settings.MaxBufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var writer = channel.Writer;
        var reader = channel.Reader;

        var producerTask = Task.Run(async () =>
        {
            try
            {
                using var semaphore = new SemaphoreSlim(_settings.MaxConcurrency, _settings.MaxConcurrency);
                var activeTasks = new List<Task>();
                var index = 0;

                await foreach (var item in _source.WithCancellation(cancellationToken))
                {
                    await semaphore.WaitAsync(cancellationToken);
                    var currentIndex = index++;

                    var processingTask = ProcessItemAsync(item, currentIndex, writer, semaphore, cancellationToken);
                    activeTasks.Add(processingTask);

                    activeTasks.RemoveAll(t => t.IsCompleted);

                    if (activeTasks.Count >= _settings.MaxBufferSize / 2)
                    {
                        await Task.WhenAny(activeTasks);
                        activeTasks.RemoveAll(t => t.IsCompleted);
                    }
                }

                await Task.WhenAll(activeTasks);
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
                throw;
            }
            finally
            {
                writer.TryComplete();
            }
        }, cancellationToken);

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var result))
                {
                    yield return result;
                }
            }
        }
        finally
        {
            try { await producerTask; } catch { }
        }
    }

    private async Task ProcessItemAsync(TSource item, int index, ChannelWriter<TResult> writer, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.OperationTimeout);

            var result = _useIndex
                 ? await _indexedSelector(item, index).WaitAsync(timeoutCts.Token)
                 : await _selector(item).WaitAsync(timeoutCts.Token);
            await writer.WriteAsync(result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (_settings.ContinueOnError)
        {
            // Skip failed items
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public override ParallelAsyncQuery<TResult> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        var newSource = _source.CloneWithNewSettings(settings);
        return _useIndex
            ? new SelectParallelAsyncQuery<TSource, TResult>(newSource, _indexedSelector)
            : new SelectParallelAsyncQuery<TSource, TResult>(newSource, _selector);
    }

    private CancellationToken CombineTokens(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default)
            return cancellationToken;
        if (cancellationToken == default)
            return _settings.CancellationToken;

        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken).Token;
    }
}

// Where operation implementation
internal class WhereParallelAsyncQuery<TSource> : ParallelAsyncQuery<TSource>
{
    private readonly ParallelAsyncQuery<TSource> _source;
    private readonly Func<TSource, Task<bool>> _predicate;

    public WhereParallelAsyncQuery(ParallelAsyncQuery<TSource> source, Func<TSource, Task<bool>> predicate)
        : base(source.Settings)
    {
        _source = source;
        _predicate = predicate;
    }

    public override async IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var combinedToken = CombineTokens(cancellationToken);

        if (_settings.ExecutionMode == ParallelExecutionMode.Sequential)
        {
            await foreach (var item in _source.WithCancellation(combinedToken))
            {
               
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken);
                timeoutCts.CancelAfter(_settings.OperationTimeout);

                if (await _predicate(item).WaitAsync(timeoutCts.Token))
                {
                    yield return item;
                }
               
            }
            yield break;
        }

        using var semaphore = new SemaphoreSlim(_settings.MaxConcurrency, _settings.MaxConcurrency);
        var channel = Channel.CreateBounded<TSource>(new BoundedChannelOptions(_settings.MaxBufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var writer = channel.Writer;
        var reader = channel.Reader;

        var producerTask = Task.Run(async () =>
        {
            try
            {
                var activeTasks = new List<Task>();

                await foreach (var item in _source.WithCancellation(combinedToken))
                {
                    await semaphore.WaitAsync(combinedToken);

                    var processingTask = ProcessPredicateAsync(item, writer, semaphore, combinedToken);
                    activeTasks.Add(processingTask);

                    activeTasks.RemoveAll(t => t.IsCompleted);

                    if (activeTasks.Count >= _settings.MaxBufferSize / 2)
                    {
                        await Task.WhenAny(activeTasks);
                        activeTasks.RemoveAll(t => t.IsCompleted);
                    }
                }

                await Task.WhenAll(activeTasks);
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
                throw;
            }
            finally
            {
                writer.TryComplete();
            }
        }, combinedToken);

        try
        {
            while (await reader.WaitToReadAsync(combinedToken))
            {
                while (reader.TryRead(out var result))
                {
                    yield return result;
                }
            }
        }
        finally
        {
            try { await producerTask; } catch { }
        }
    }

    private async Task ProcessPredicateAsync(TSource item, ChannelWriter<TSource> writer, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.OperationTimeout);

            if (await _predicate(item).WaitAsync(timeoutCts.Token))
            {
                await writer.WriteAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (_settings.ContinueOnError)
        {
            // Skip failed items
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public override ParallelAsyncQuery<TSource> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        var newSource = _source.CloneWithNewSettings(settings);
        return new WhereParallelAsyncQuery<TSource>(newSource, _predicate);
    }

    private CancellationToken CombineTokens(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default)
            return cancellationToken;
        if (cancellationToken == default)
            return _settings.CancellationToken;

        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken).Token;
    }
}

// Take operation implementation
internal class TakeParallelAsyncQuery<TSource> : ParallelAsyncQuery<TSource>
{
    private readonly ParallelAsyncQuery<TSource> _source;
    private readonly int _count;

    public TakeParallelAsyncQuery(ParallelAsyncQuery<TSource> source, int count)
        : base(source.Settings)
    {
        _source = source;
        _count = count;
    }

    public override async IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var combinedToken = CombineTokens(cancellationToken);
        var taken = 0;

        await foreach (var item in _source.WithCancellation(combinedToken))
        {
            if (taken >= _count)
                break;

            yield return item;
            taken++;
        }
    }

    public override ParallelAsyncQuery<TSource> CloneWithNewSettings(ParallelExecutionSettings settings)
    {
        var newSource = _source.CloneWithNewSettings(settings);
        return new TakeParallelAsyncQuery<TSource>(newSource, _count);
    }

    private CancellationToken CombineTokens(CancellationToken cancellationToken)
    {
        if (_settings.CancellationToken == default)
            return cancellationToken;
        if (cancellationToken == default)
            return _settings.CancellationToken;

        return CancellationTokenSource.CreateLinkedTokenSource(_settings.CancellationToken, cancellationToken).Token;
    }
}

// Extension methods
public static class ParallelAsyncEnumerableExtensions
{
    public static ParallelAsyncQuery<TSource> AsParallel<TSource>(this IAsyncEnumerable<TSource> source)
    {
        return new SourceParallelAsyncQuery<TSource>(source, new ParallelExecutionSettings());
    }

    public static ParallelAsyncQuery<TSource> AsParallel<TSource>(this IAsyncEnumerable<TSource> source, ParallelExecutionSettings settings)
    {
        return new SourceParallelAsyncQuery<TSource>(source, settings);
    }

    public static ParallelAsyncQuery<TResult> Select<TSource, TResult>(this ParallelAsyncQuery<TSource> source, Func<TSource, TResult> selector)
    {
        return new SelectParallelAsyncQuery<TSource, TResult>(source, item => Task.FromResult(selector(item)));
    }

    public static ParallelAsyncQuery<TResult> Select<TSource, TResult>(this ParallelAsyncQuery<TSource> source, Func<TSource, Task<TResult>> selector)
    {
        return new SelectParallelAsyncQuery<TSource, TResult>(source, selector);
    }

    public static ParallelAsyncQuery<TSource> Where<TSource>(this ParallelAsyncQuery<TSource> source, Func<TSource, bool> predicate)
    {
        return new WhereParallelAsyncQuery<TSource>(source, item => Task.FromResult(predicate(item)));
    }

    public static ParallelAsyncQuery<TSource> Where<TSource>(this ParallelAsyncQuery<TSource> source, Func<TSource, Task<bool>> predicate)
    {
        return new WhereParallelAsyncQuery<TSource>(source, predicate);
    }

    public static ParallelAsyncQuery<TSource> Take<TSource>(this ParallelAsyncQuery<TSource> source, int count)
    {
        return new TakeParallelAsyncQuery<TSource>(source, count);
    }

    public static async Task<List<TSource>> ToListAsync<TSource>(this ParallelAsyncQuery<TSource> source, CancellationToken cancellationToken = default)
    {
        var result = new List<TSource>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }
        return result;
    }

    public static async Task<TSource[]> ToArrayAsync<TSource>(this ParallelAsyncQuery<TSource> source, CancellationToken cancellationToken = default)
    {
        var list = await source.ToListAsync(cancellationToken);
        return list.ToArray();
    }

    public static async Task<int> CountAsync<TSource>(this ParallelAsyncQuery<TSource> source, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var _ in source.WithCancellation(cancellationToken))
        {
            count++;
        }
        return count;
    }

    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Action<TSource> action, CancellationToken cancellationToken = default)
    //{
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        action(item);
    //    }
    //}

    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Func<TSource, Task> action, CancellationToken cancellationToken = default)
    //{
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        await action(item);
    //    }
    //}

    public static ParallelAsyncQuery<TResult> Select<TSource, TResult>(this ParallelAsyncQuery<TSource> source, Func<TSource, int, TResult> selector)
    {
        return new SelectParallelAsyncQuery<TSource, TResult>(source, (item, index) => Task.FromResult(selector(item, index)));
    }

    public static ParallelAsyncQuery<TResult> Select<TSource, TResult>(this ParallelAsyncQuery<TSource> source, Func<TSource, int, Task<TResult>> selector)
    {
        return new SelectParallelAsyncQuery<TSource, TResult>(source, selector);
    }

    // ForEach methods that return the original items (pass-through with side effects)
    //public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T> action)
    //{
    //    return source.Select(x =>
    //    {
    //        action(x);
    //        return x;
    //    });
    //}

    //public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T, int> action)
    //{
    //    return source.Select((x, idx) =>
    //    {
    //        action(x, idx);
    //        return x;
    //    });
    //}

    //public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, Task> action)
    //{
    //    return source.Select(async x =>
    //    {
    //        await action(x);
    //        return x;
    //    });
    //}

    //public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, int, Task> action)
    //{
    //    return source.Select(async (x, idx) =>
    //    {
    //        await action(x, idx);
    //        return x;
    //    });
    //}

    // Terminal ForEach operations (don't return items, just execute actions)
    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Action<TSource> action, CancellationToken cancellationToken = default)
    //{
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        action(item);
    //    }
    //}

    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Action<TSource, int> action, CancellationToken cancellationToken = default)
    //{
    //    var index = 0;
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        action(item, index++);
    //    }
    //}

    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Func<TSource, Task> action, CancellationToken cancellationToken = default)
    //{
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        await action(item);
    //    }
    //}

    //public static async Task ForEachAsync<TSource>(this ParallelAsyncQuery<TSource> source, Func<TSource, int, Task> action, CancellationToken cancellationToken = default)
    //{
    //    var index = 0;
    //    await foreach (var item in source.WithCancellation(cancellationToken))
    //    {
    //        await action(item, index++);
    //    }
    //}
}

// Helper extensions for regular IAsyncEnumerable
public static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }
        return result;
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
