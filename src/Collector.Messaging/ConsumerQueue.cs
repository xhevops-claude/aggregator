namespace Collector.Messaging;

public sealed record ConsumerQueue(string Name, string Message, ushort Prefetch, byte? MaxPriority = null);
