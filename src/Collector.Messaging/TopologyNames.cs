namespace Collector.Messaging;

public static class TopologyNames
{
    public static string Exchange(string prefix) => prefix;

    public static string DeadExchange(string prefix) => $"{prefix}.dead";

    public static string Queue(string prefix, string queue) => $"{prefix}.{queue}";

    public static string RetryQueue(string prefix, string queue) => $"{Queue(prefix, queue)}.retry";

    public static string DeadQueue(string prefix, string queue) => $"{Queue(prefix, queue)}.dead";
}
