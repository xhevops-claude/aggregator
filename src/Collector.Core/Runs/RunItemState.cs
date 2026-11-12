namespace Collector.Core.Runs;

public enum RunItemState { Queued, Running, Done, Failed, Retrying, Skipped, WaitingForDecision }
