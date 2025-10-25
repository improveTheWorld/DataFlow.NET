
namespace DataFlow.Framework;
public enum UnifyErrorMode
{
    FailFast,          // Any source error ends the merged stream (default)
    ContinueOnError    // Drop failing source, continue with others
}

public enum UnifyFairness
{
    FirstAvailable,    // Yield whichever source produces next (default)
    RoundRobin         // Aim for fairness across sources (best-effort without buffering)
}

public sealed class UnifyOptions
{
    public UnifyErrorMode ErrorMode { get; init; } = UnifyErrorMode.FailFast;
    public UnifyFairness Fairness { get; init; } = UnifyFairness.FirstAvailable;
}

// New unified merger: AsyncEnumerable<T> (aka DataFlow)
public sealed class AsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly List<SourceEntry> _sources = new();
    private readonly UnifyOptions _options;

    private bool _frozen; // Prevent mutation after enumeration starts
    private int _activeEnumerations; // For guarding disposal if needed later

    public AsyncEnumerable(UnifyOptions? options = null)
    {
        _options = options ?? new UnifyOptions();
    }

    // Merge a source (aka ListenTo)
    public AsyncEnumerable<T> Unify(IAsyncEnumerable<T> source, string name, Func<T, bool>? predicate = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (name is null) throw new ArgumentNullException(nameof(name));
        ThrowIfFrozen();
        _sources.Add(new SourceEntry(name, source, predicate));
        return this;
    }

    // Remove a source by name (only before enumeration starts)
    public bool Unlisten(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        ThrowIfFrozen();
        var idx = _sources.FindIndex(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (idx >= 0)
        {
            _sources.RemoveAt(idx);
            return true;
        }
        return false;
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_sources.Count == 0)
            yield break;

        Interlocked.Increment(ref _activeEnumerations);
        _frozen = true;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedCts.Token;

        var states = new List<SourceState>(_sources.Count);
        try
        {
            foreach (var s in _sources)
            {
                var e = s.Source.GetAsyncEnumerator(token);
                states.Add(new SourceState(s.Name, e, s.Predicate));
            }

            foreach (var st in states)
                st.ArmNext();

            if (_options.Fairness == UnifyFairness.FirstAvailable)
            {
                while (states.Count > 0)
                {
                    var completed = await Task.WhenAny(states.Select(x => x.MoveNextTask!)).ConfigureAwait(false);
                    var idx = states.FindIndex(x => x.MoveNextTask == completed);
                    if (idx < 0)
                        continue;

                    var st = states[idx];

                    bool hasItem;
                    try
                    {
                        hasItem = await st.MoveNextTask!.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (_options.ErrorMode == UnifyErrorMode.FailFast)
                            throw WrapSourceError(st.Name, ex);
                        await st.DisposeAsync().ConfigureAwait(false);
                        states.RemoveAt(idx);
                        continue;
                    }

                    if (!hasItem)
                    {
                        await st.DisposeAsync().ConfigureAwait(false);
                        states.RemoveAt(idx);
                        continue;
                    }

                    var current = st.Enumerator.Current;
                    if (st.Predicate is null || st.Predicate(current))
                    {
                        yield return current;
                    }

                    st.ArmNext();
                }
            }
            else
            {
                int index = 0;
                while (states.Count > 0)
                {
                    if (index >= states.Count) index = 0;

                    var st = states[index];

                    Task<bool> task = st.MoveNextTask!;
                    bool hasItem;
                    try
                    {
                        if (task.IsCompleted)
                            hasItem = task.GetAwaiter().GetResult();
                        else
                            hasItem = await task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (_options.ErrorMode == UnifyErrorMode.FailFast)
                            throw WrapSourceError(st.Name, ex);
                        await st.DisposeAsync().ConfigureAwait(false);
                        states.RemoveAt(index);
                        continue;
                    }

                    if (!hasItem)
                    {
                        await st.DisposeAsync().ConfigureAwait(false);
                        states.RemoveAt(index);
                        continue;
                    }

                    var current = st.Enumerator.Current;
                    if (st.Predicate is null || st.Predicate(current))
                    {
                        yield return current;
                    }

                    st.ArmNext();
                    index++;
                }
            }
        }
        finally
        {
            foreach (var st in states)
            {
                try { await st.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort cleanup */ }
            }

            if (Interlocked.Decrement(ref _activeEnumerations) == 0)
            {
                // Keep frozen to discourage dynamic changes
                // _frozen = false;
            }
        }
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("Cannot modify AsyncEnumerable after enumeration has started.");
    }

    private static Exception WrapSourceError(string name, Exception ex)
        => new InvalidOperationException($"AsyncEnumerable source '{name}' failed.", ex);

    private readonly struct SourceEntry
    {
        public string Name { get; }
        public IAsyncEnumerable<T> Source { get; }
        public Func<T, bool>? Predicate { get; }

        public SourceEntry(string name, IAsyncEnumerable<T> source, Func<T, bool>? predicate)
        {
            Name = name;
            Source = source;
            Predicate = predicate;
        }
    }

    private sealed class SourceState : IAsyncDisposable
    {
        public string Name { get; }
        public IAsyncEnumerator<T> Enumerator { get; }
        public Func<T, bool>? Predicate { get; }

        public Task<bool>? MoveNextTask { get; private set; }

        public SourceState(string name, IAsyncEnumerator<T> enumerator, Func<T, bool>? predicate)
        {
            Name = name;
            Enumerator = enumerator;
            Predicate = predicate;
        }

        public void ArmNext()
        {
            MoveNextTask = Enumerator.MoveNextAsync().AsTask();
        }

        public async ValueTask DisposeAsync()
        {
            try { await Enumerator.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort cleanup */ }
        }
    }
}