using Collector.Core.Platform;

namespace Collector.Cli;

public sealed record Leaf(Operation Operation, Source Source, string Domain);
