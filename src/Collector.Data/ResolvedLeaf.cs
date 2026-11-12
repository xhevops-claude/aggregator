using Collector.Core.Platform;

namespace Collector.Data;

public sealed record ResolvedLeaf(Project Project, Operation Operation, Source Source, string Domain);
