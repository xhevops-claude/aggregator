namespace Collector.Lake;

public sealed record LakeOptions
{
    public const string RootVariable = "COLLECTOR_LAKE";
    public const string DefaultFolder = ".lake";

    public static readonly TimeSpan DefaultUnindexedBodyWait = TimeSpan.FromSeconds(2);

    public string Root { get; init; } = "";
    public TimeSpan UnindexedBodyWait { get; init; } = DefaultUnindexedBodyWait;

    public static LakeOptions FromEnvironment(string repositoryRoot)
    {
        var root = Environment.GetEnvironmentVariable(RootVariable);
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(repositoryRoot, DefaultFolder);
        return new LakeOptions { Root = Path.GetFullPath(root) };
    }
}
