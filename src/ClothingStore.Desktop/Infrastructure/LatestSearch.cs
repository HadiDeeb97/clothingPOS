namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Runs searches so that only the newest one counts. Starting a search cancels the one before it, and the
/// results of a search that was overtaken are thrown away instead of replacing newer results on screen.
/// (Searching on every keystroke without this lets a slow query for "je" finish after the query for "jeans".)
/// </summary>
public sealed class LatestSearch(TimeSpan delay)
{
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

    private CancellationTokenSource? _current;

    public LatestSearch() : this(TypingDelay)
    {
    }

    /// <summary>Waits for typing to pause, loads, and applies the result unless a newer search started meanwhile.</summary>
    /// <returns>False if this search was overtaken by a newer one.</returns>
    public Task<bool> RunAsync<T>(Func<CancellationToken, Task<T>> load, Action<T> apply) => RunAsync(load, apply, delay);

    /// <summary>Same as <see cref="RunAsync{T}(Func{CancellationToken, Task{T}}, Action{T})"/> without the typing pause (Enter, a scan, a filter change).</summary>
    public Task<bool> RunNowAsync<T>(Func<CancellationToken, Task<T>> load, Action<T> apply) => RunAsync(load, apply, TimeSpan.Zero);

    /// <summary>Cancels any search in progress.</summary>
    public void Cancel()
    {
        _current?.Cancel();
        _current = null;
    }

    private async Task<bool> RunAsync<T>(Func<CancellationToken, Task<T>> load, Action<T> apply, TimeSpan wait)
    {
        _current?.Cancel();
        var cts = new CancellationTokenSource();
        _current = cts;
        try
        {
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cts.Token);
            var result = await load(cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_current, cts)) return false;
            apply(result);
            return true;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            if (ReferenceEquals(_current, cts)) _current = null;
            cts.Dispose();
        }
    }
}
