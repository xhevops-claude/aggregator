using Collector.Core.Runs;

namespace Collector.Cli.Commands;

public sealed record RunOutcome(RunItemState ItemState, RunState RunState, string? ErrorClass, string? ErrorText)
{
    public const string CancelledClass = "cancelled";

    public static RunOutcome Completed(bool succeeded, string errorClass, string? errorText)
    {
        if (succeeded) return new RunOutcome(RunItemState.Done, RunState.Done, null, null);
        return new RunOutcome(RunItemState.Failed, RunState.WaitingForDecision, errorClass, errorText);
    }

    public static RunOutcome Aborted(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return new RunOutcome(RunItemState.Failed, RunState.Aborted, CancelledClass, "cancelled by the operator");
        }

        return new RunOutcome(RunItemState.Failed, RunState.Aborted, exception.GetType().Name, exception.Message);
    }
}
