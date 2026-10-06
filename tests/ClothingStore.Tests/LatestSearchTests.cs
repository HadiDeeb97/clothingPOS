using ClothingStore.Desktop.Infrastructure;

namespace ClothingStore.Tests;

public class LatestSearchTests
{
    [Fact]
    public async Task A_slow_older_search_never_overwrites_a_newer_one()
    {
        var search = new LatestSearch(TimeSpan.Zero);
        var slow = new TaskCompletionSource<string>();
        var shown = new List<string>();

        var first = search.RunAsync(_ => slow.Task, shown.Add);              // "je" — slow query
        var second = search.RunAsync(_ => Task.FromResult("jeans"), shown.Add); // "jeans" — fast query
        slow.SetResult("je");                                                // the old query finishes last

        Assert.False(await first);
        Assert.True(await second);
        Assert.Equal(["jeans"], shown);
    }

    [Fact]
    public async Task Typing_quickly_only_runs_the_last_search()
    {
        var search = new LatestSearch(TimeSpan.FromMilliseconds(50));
        var queried = new List<string>();
        var shown = new List<string>();

        var runs = new[] { "j", "je", "jea", "jeans" }
            .Select(text => search.RunAsync(_ => { queried.Add(text); return Task.FromResult(text); }, shown.Add))
            .ToList();
        await Task.WhenAll(runs);

        Assert.Equal(["jeans"], queried);
        Assert.Equal(["jeans"], shown);
    }

    [Fact]
    public async Task Run_now_skips_the_typing_pause_and_cancel_discards_a_running_search()
    {
        var search = new LatestSearch(TimeSpan.FromHours(1));
        var shown = new List<string>();

        Assert.True(await search.RunNowAsync(_ => Task.FromResult("scan"), shown.Add));

        var pending = new TaskCompletionSource<string>();
        var running = search.RunNowAsync(_ => pending.Task, shown.Add);
        search.Cancel();
        pending.SetResult("late");

        Assert.False(await running);
        Assert.Equal(["scan"], shown);
    }

    [Fact]
    public async Task The_query_receives_a_token_that_is_cancelled_when_overtaken()
    {
        var search = new LatestSearch(TimeSpan.Zero);
        CancellationToken firstToken = default;
        var gate = new TaskCompletionSource<string>();

        var first = search.RunAsync(ct => { firstToken = ct; return gate.Task; }, _ => { });
        _ = search.RunAsync(_ => Task.FromResult("newer"), _ => { });

        Assert.True(firstToken.IsCancellationRequested);
        gate.SetResult("old");
        Assert.False(await first);
    }
}
