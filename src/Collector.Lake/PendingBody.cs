namespace Collector.Lake;

internal sealed class PendingBody(string folder, byte[] compressed) : IDisposable
{
    private string? temp;

    public async Task<string> PrepareAsync(CancellationToken token)
    {
        temp ??= await LakeBody.WriteTempAsync(folder, compressed, token);
        return temp;
    }

    public async Task<bool> TryPlaceAsync(string path, CancellationToken token)
    {
        var prepared = await PrepareAsync(token);
        if (!LakeBody.TryMoveIntoPlace(prepared, path)) return false;

        temp = null;
        return true;
    }

    public void Dispose()
    {
        if (temp is not null) LakeBody.TryDelete(temp);
        temp = null;
    }
}
