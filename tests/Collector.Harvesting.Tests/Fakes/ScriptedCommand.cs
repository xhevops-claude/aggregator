using System.Runtime.CompilerServices;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class ScriptedCommand : IHarvestCommand
{
    private readonly List<HarvestItem> items;
    private readonly Exception? failure;

    public ScriptedCommand(string name, IEnumerable<HarvestItem> items)
        : this(name, items, null)
    {
    }

    public ScriptedCommand(string name, IEnumerable<HarvestItem> items, Exception? failure)
    {
        Name = name;
        this.items = items.ToList();
        this.failure = failure;
    }

    public string Name { get; }

    public string Platform => "test";

    public HarvestRequest? Request { get; private set; }

    public async IAsyncEnumerable<HarvestItem> RunAsync(HarvestRequest request, IFetcher fetcher,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Request = request;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }

        if (failure is not null) throw failure;
    }
}
