using Collector.Core.Platform;

namespace Collector.Lake.Tests;

public static class ConcurrentWrites
{
    public static async Task<LakeWriteResult[]> RunAsync(
        IReadOnlyList<LakeStore> stores, IReadOnlyList<LakeWrite> writes, LakeRetention retention)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new List<Task<LakeWriteResult>>();
        for (var i = 0; i < writes.Count; i++)
        {
            var store = stores[i % stores.Count];
            var write = writes[i];
            tasks.Add(Task.Run(() => WriteWhenStartedAsync(store, write)));
        }

        start.SetResult();
        return await Task.WhenAll(tasks);

        async Task<LakeWriteResult> WriteWhenStartedAsync(LakeStore store, LakeWrite write)
        {
            await start.Task;
            return await store.WriteAsync(write, retention, CancellationToken.None);
        }
    }
}
