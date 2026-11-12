using System.Text;
using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Core.Runs;
using Collector.Harvesting.Tests.Fakes;

namespace Collector.Harvesting.Tests;

public class HarvestRunnerTests
{
    private const string CommandName = "shop.pages";

    private static readonly Guid ProjectId = Guid.Parse("0b7c1f8e-2d41-4c55-9f3a-1e2d3c4b5a69");
    private static readonly Guid RunId = Guid.Parse("1a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Guid ItemId = Guid.Parse("1a2b3c4d-0000-4000-8000-000000000002");
    private static readonly DateTime HarvestedAt = new(2026, 11, 12, 7, 30, 0, DateTimeKind.Utc);

    private readonly Clock clock = new();
    private readonly InMemoryLake lake = new();
    private readonly RecordingPublisher publisher = new();
    private readonly RecordingFetcher fetcher;
    private readonly HostBudget budget;

    public HarvestRunnerTests()
    {
        fetcher = new RecordingFetcher(clock.Time);
        budget = new HostBudget(clock.Time);
    }

    [Fact]
    public async Task Stores_each_item_and_signals_each_new_version()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two")]);

        var report = await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        Assert.True(report.Succeeded);
        Assert.Equal(["p1", "p2"], report.Stored.Select(item => item.ProviderId));
        Assert.Equal(["p1", "p2"], lake.Writes.Select(write => write.ProviderId));
        var write = lake.Writes[0];
        Assert.Equal(ProjectId, write.ProjectId);
        Assert.Equal("shop_test", write.Source);
        Assert.Equal("product-page", write.Kind);
        Assert.Equal("https://shop.test/p/p1", write.Url);
        Assert.Equal("text/html", write.ContentType);
        Assert.Equal(HarvestedAt, write.HarvestedAt);
        Assert.Equal("one", Encoding.UTF8.GetString(write.Body));
        string[] names = [MessageNames.LakeItemStored, MessageNames.LakeItemStored, MessageNames.ItemCompleted];
        Assert.Equal(names, publisher.Names);
    }

    [Fact]
    public async Task The_stored_signal_carries_the_lake_key_version_hash_and_size()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);

        var report = await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        var stored = Assert.Single(publisher.Of<LakeItemStored>(MessageNames.LakeItemStored));
        Assert.Equal(ProjectId, stored.ProjectId);
        Assert.Equal(RunId, stored.RunId);
        Assert.Equal(ItemId, stored.ItemId);
        Assert.Equal($"{ProjectId}/shop_test/product-page/p1", stored.Key);
        Assert.Equal(1, stored.Version);
        Assert.Equal(64, stored.Hash.Length);
        Assert.Equal(3, stored.Size);
        Assert.Equal(stored.Key, report.Stored[0].Key);
    }

    [Fact]
    public async Task A_clean_harvest_completes_the_item_as_done()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);

        await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Done, completed.State);
        Assert.Equal(ProjectId, completed.ProjectId);
        Assert.Equal(RunId, completed.RunId);
        Assert.Equal(ItemId, completed.ItemId);
        Assert.Null(completed.ErrorClass);
        Assert.Null(completed.ErrorText);
    }

    [Fact]
    public async Task Failed_items_are_collected_per_item_and_the_rest_still_stored()
    {
        var failed = Item("p2", "") with { ErrorClass = "http", ErrorText = "404 Not Found" };
        var command = new ScriptedCommand(CommandName, [Item("p1", "one"), failed, Item("p3", "three")]);

        var report = await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        Assert.Equal(["p1", "p3"], lake.Writes.Select(write => write.ProviderId));
        var failure = Assert.Single(report.Failed);
        Assert.Equal("p2", failure.ProviderId);
        Assert.Equal("http", failure.ErrorClass);
        Assert.Equal("404 Not Found", failure.ErrorText);
        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Failed, completed.State);
        Assert.Equal("harvest", completed.ErrorClass);
        Assert.Equal("1 of 3 items failed: p2 (http: 404 Not Found)", completed.ErrorText);
    }

    [Fact]
    public async Task Unchanged_items_under_changes_only_send_no_stored_signal()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two")]);
        var runner = Runner(command);
        await runner.RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);
        publisher.Published.Clear();

        var report = await runner.RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);

        Assert.Empty(report.Stored);
        Assert.Equal(["p1", "p2"], report.Unchanged.Select(item => item.ProviderId));
        Assert.Equal([MessageNames.ItemCompleted], publisher.Names);
        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Done, completed.State);
        Assert.Equal(report.Unchanged.Select(item => item.Key), completed.LakeKeys);
    }

    [Fact]
    public async Task The_completion_names_every_stored_and_unchanged_key_once()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two")]);
        var runner = Runner(command);
        await runner.RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);
        publisher.Published.Clear();
        var changed = Runner(new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "new")]));

        var report = await changed.RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);

        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        var expected = report.Stored.Concat(report.Unchanged).Select(item => item.Key).Order();
        Assert.Equal(expected, completed.LakeKeys.Order());
        Assert.Equal(2, completed.LakeKeys.Count);
    }

    [Fact]
    public async Task Only_the_changed_item_is_signalled_under_changes_only()
    {
        var first = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two")]);
        await Runner(first).RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);
        publisher.Published.Clear();
        var second = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two, now cheaper")]);

        var report = await Runner(second).RunAsync(Message(), Source(), LakeRetention.ChangesOnly, default);

        var stored = Assert.Single(publisher.Of<LakeItemStored>(MessageNames.LakeItemStored));
        Assert.EndsWith("/p2", stored.Key);
        Assert.Equal(2, stored.Version);
        Assert.Equal("p1", Assert.Single(report.Unchanged).ProviderId);
    }

    [Fact]
    public async Task A_lake_write_failure_fails_only_that_item()
    {
        lake.Broken.Add("p2");
        var command = new ScriptedCommand(CommandName, [Item("p1", "one"), Item("p2", "two"), Item("p3", "three")]);

        var report = await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        Assert.Equal(["p1", "p3"], report.Stored.Select(item => item.ProviderId));
        var failure = Assert.Single(report.Failed);
        Assert.Equal("p2", failure.ProviderId);
        Assert.Equal("lake", failure.ErrorClass);
        Assert.Equal("disk full", failure.ErrorText);
        Assert.Equal(2, publisher.Of<LakeItemStored>(MessageNames.LakeItemStored).Count);
    }

    [Fact]
    public async Task A_command_that_throws_keeps_what_it_stored_and_fails_the_item()
    {
        var blocked = new RobotsDisallowedException(new Uri("https://shop.test/private"));
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")], blocked);

        var report = await Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, default);

        Assert.Equal("p1", Assert.Single(report.Stored).ProviderId);
        Assert.Equal("RobotsDisallowedException: robots.txt disallows https://shop.test/private", report.Error);
        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Failed, completed.State);
        Assert.Equal("harvest", completed.ErrorClass);
        Assert.StartsWith("command stopped: RobotsDisallowedException", completed.ErrorText);
    }

    [Fact]
    public async Task An_unknown_command_fails_the_item()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);
        var message = Message() with { Command = "shop.nothing" };

        var report = await Runner(command).RunAsync(message, Source(), LakeRetention.FullHistory, default);

        Assert.Equal("unknown harvest command 'shop.nothing'", report.Error);
        Assert.Empty(lake.Writes);
        var completed = Assert.Single(publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Failed, completed.State);
        Assert.Equal("command stopped: unknown harvest command 'shop.nothing'", completed.ErrorText);
    }

    [Fact]
    public async Task The_command_gets_the_targets_parameters_and_limit()
    {
        var command = new ScriptedCommand(CommandName, []);
        var target = new HarvestTarget { ProviderId = "4711", Url = "https://shop.test/p/4711" };
        var message = Message() with
        {
            Targets = [target],
            Parameters = new() { [HarvestParameters.Brand] = "adidas" },
            Limit = 3,
        };
        var source = Source();

        await Runner(command).RunAsync(message, source, LakeRetention.FullHistory, default);

        var request = command.Request!;
        Assert.Equal(ProjectId, request.ProjectId);
        Assert.Equal("clothing/shoes/shop", request.Namespace);
        Assert.Same(source, request.Source);
        Assert.Equal("4711", Assert.Single(request.Targets).ProviderId);
        Assert.Equal("adidas", request.Parameter(HarvestParameters.Brand));
        Assert.Equal(3, request.Limit);
    }

    [Fact]
    public async Task Fetches_follow_the_strategy_pacing_above_the_source_floor()
    {
        var targets = Enumerable.Range(1, 3).Select(Target).ToList();
        var pacing = new Pacing { Mode = PacingMode.Interval, Interval = TimeSpan.FromSeconds(3) };
        var message = Message() with { Command = "shop.fetch", Targets = targets, Pacing = pacing };
        var source = Source() with { MinRequestInterval = TimeSpan.FromSeconds(1) };
        var runner = Runner(new FetchingCommand());
        var retention = LakeRetention.FullHistory;

        var report = await clock.Drive(() => runner.RunAsync(message, source, retention, default));

        Assert.Equal(3, report.Stored.Count);
        Assert.Equal(Times.Seconds(0, 3, 6), fetcher.Starts);
        Assert.Equal(Clock.Start.AddSeconds(6).UtcDateTime, lake.Writes[2].HarvestedAt);
    }

    [Fact]
    public async Task The_source_is_registered_with_the_fetcher()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);
        var source = Source();

        await Runner(command).RunAsync(Message(), source, LakeRetention.FullHistory, default);

        Assert.Same(source, Assert.Single(fetcher.Registered));
    }

    [Fact]
    public async Task Concurrent_runs_on_one_source_keep_its_floor()
    {
        var handler = ShopHandler(6);
        using var polite = new PoliteFetcher(handler, new FetcherOptions(), clock.Time);
        var runner = Runner(new FetchingCommand(), polite);
        var source = Source() with { MinRequestInterval = TimeSpan.FromSeconds(3) };
        var first = FetchMessage(new Pacing { Mode = PacingMode.Burst }, 1, 2, 3);
        var second = FetchMessage(new Pacing { Mode = PacingMode.Burst }, 4, 5, 6);

        await clock.Drive(() => RunTogether(runner, source, first, second));

        Assert.Equal(Times.Seconds(0, 3, 6, 9, 12, 15), handler.Times);
    }

    [Fact]
    public async Task Back_to_back_runs_on_one_source_keep_its_floor()
    {
        var handler = ShopHandler(4);
        using var polite = new PoliteFetcher(handler, new FetcherOptions(), clock.Time);
        var runner = Runner(new FetchingCommand(), polite);
        var source = Source() with { MinRequestInterval = TimeSpan.FromSeconds(3) };
        var first = FetchMessage(new Pacing { Mode = PacingMode.Burst }, 1, 2);
        var second = FetchMessage(new Pacing { Mode = PacingMode.Burst }, 3, 4);

        await clock.Drive(() => runner.RunAsync(first, source, LakeRetention.FullHistory, default));
        await clock.Drive(() => runner.RunAsync(second, source, LakeRetention.FullHistory, default));

        Assert.Equal(Times.Seconds(0, 3, 6, 9), handler.Times);
    }

    [Fact]
    public async Task A_source_that_ignores_robots_is_fetched_without_them()
    {
        var handler = ShopHandler(1);
        using var polite = new PoliteFetcher(handler, new FetcherOptions(), clock.Time);
        var runner = Runner(new FetchingCommand(), polite);
        var message = FetchMessage(new Pacing { Mode = PacingMode.Burst }, 1);

        await clock.Drive(() => runner.RunAsync(message, Source(), LakeRetention.FullHistory, default));

        Assert.Equal(["https://shop.test/p/1"], handler.Urls);
    }

    [Fact]
    public async Task Runs_on_one_host_share_one_budget()
    {
        var handler = ShopHandler(4);
        using var polite = new PoliteFetcher(handler, new FetcherOptions(), clock.Time);
        var runner = Runner(new FetchingCommand(), polite);
        var source = Source() with { MinRequestInterval = TimeSpan.FromSeconds(1) };
        var pacing = new Pacing { Mode = PacingMode.Budget, MaxPerHour = 2 };
        var first = FetchMessage(pacing, 1, 2);
        var second = FetchMessage(pacing, 3, 4);

        await clock.Drive(() => runner.RunAsync(first, source, LakeRetention.FullHistory, default));
        await clock.Drive(() => runner.RunAsync(second, source, LakeRetention.FullHistory, default));

        Assert.Equal(Times.Seconds(0, 1, 3600, 3601), handler.Times);
    }

    [Fact]
    public async Task A_source_that_does_not_match_the_command_is_rejected()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);
        var other = Source() with { Key = "other_shop" };

        var run = Runner(command).RunAsync(Message(), other, LakeRetention.FullHistory, default);

        await Assert.ThrowsAsync<ArgumentException>(() => run);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Cancellation_stops_without_completing_the_item()
    {
        var command = new ScriptedCommand(CommandName, [Item("p1", "one")]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var run = Runner(command).RunAsync(Message(), Source(), LakeRetention.FullHistory, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Empty(publisher.Published);
    }

    private HarvestRunner Runner(IHarvestCommand command)
    {
        return Runner(command, fetcher);
    }

    private HarvestRunner Runner(IHarvestCommand command, ISourceFetcher sourceFetcher)
    {
        var catalog = new HarvestCommandCatalog([command]);
        return new HarvestRunner(catalog, sourceFetcher, budget, lake, publisher, clock.Time);
    }

    private FakeHandler ShopHandler(int pages)
    {
        var handler = new FakeHandler(clock.Time);
        for (var number = 1; number <= pages; number++) handler.On($"https://shop.test/p/{number}", Respond.Ok());
        return handler;
    }

    private static HarvestCommand FetchMessage(Pacing pacing, params int[] pages)
    {
        var targets = pages.Select(Target).ToList();
        return Message() with { ItemId = Guid.NewGuid(), Command = "shop.fetch", Targets = targets, Pacing = pacing };
    }

    private static async Task<int> RunTogether(HarvestRunner runner, Source source, params HarvestCommand[] messages)
    {
        var runs = messages.Select(message => runner.RunAsync(message, source, LakeRetention.FullHistory, default));
        var reports = await Task.WhenAll(runs);
        return reports.Sum(report => report.Stored.Count);
    }

    private static HarvestCommand Message()
    {
        return new HarvestCommand
        {
            ProjectId = ProjectId,
            RunId = RunId,
            ItemId = ItemId,
            Namespace = "clothing/shoes/shop",
            Command = CommandName,
            Source = "shop_test",
            Pacing = new Pacing { Mode = PacingMode.Burst },
        };
    }

    private static Source Source()
    {
        return new Source
        {
            ProjectId = ProjectId,
            Key = "shop_test",
            Provider = "shop",
            BaseUrl = "https://shop.test",
            MinRequestInterval = TimeSpan.Zero,
            RespectRobots = false,
        };
    }

    private static HarvestItem Item(string providerId, string body)
    {
        return new HarvestItem
        {
            Kind = "product-page",
            ProviderId = providerId,
            Url = $"https://shop.test/p/{providerId}",
            ContentType = "text/html",
            Body = Encoding.UTF8.GetBytes(body),
            HarvestedAt = HarvestedAt,
        };
    }

    private static HarvestTarget Target(int number)
    {
        return new HarvestTarget { ProviderId = $"p{number}", Url = $"https://shop.test/p/{number}" };
    }
}
