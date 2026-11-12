using System.Runtime.CompilerServices;

namespace Collector.Harvesting.Tests;

internal static class SampleFile
{
    public static string Read(string name, [CallerFilePath] string caller = "")
    {
        var folder = Path.GetDirectoryName(caller) ?? "";
        return File.ReadAllText(Path.Combine(folder, "Samples", name));
    }
}
