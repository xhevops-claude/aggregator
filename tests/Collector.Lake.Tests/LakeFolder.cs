using Collector.Core.Lake;

namespace Collector.Lake.Tests;

public static class LakeFolder
{
    public static string Of(LakeStore store, LakeWrite write)
    {
        var key = new LakeKey(write.ProjectId, write.Source, write.Kind, write.ProviderId);
        return Path.GetFullPath(Path.Combine(store.Root, LakePath.Folder(key)));
    }

    public static string[] Bodies(string folder) => Files(folder, "*.gz");

    public static string[] Temps(string folder) => Files(folder, "*.tmp");

    private static string[] Files(string folder, string pattern)
    {
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder, pattern);
    }
}
