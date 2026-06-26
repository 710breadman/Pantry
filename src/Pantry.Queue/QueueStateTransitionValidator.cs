namespace Pantry.Queue;

public sealed class QueueStateTransitionValidator
{
    private static readonly HashSet<QueueJobStatus> TerminalStatuses =
    [
        QueueJobStatus.Succeeded,
        QueueJobStatus.Failed,
        QueueJobStatus.Cancelled,
        QueueJobStatus.Skipped
    ];

    public QueueStateTransitionResult Validate(
        QueueJobStatus currentStatus,
        QueueJobStatus nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return Allowed("Status is unchanged.");
        }

        if (TerminalStatuses.Contains(currentStatus))
        {
            return Rejected("Terminal jobs cannot change status.");
        }

        return currentStatus switch
        {
            QueueJobStatus.Planned => ValidateFromPlanned(nextStatus),
            QueueJobStatus.WaitingForReview => ValidateFromWaitingForReview(nextStatus),
            QueueJobStatus.WaitingForDependencies => ValidateFromWaitingForDependencies(nextStatus),
            QueueJobStatus.Running => ValidateFromRunning(nextStatus),
            _ => Rejected("Unknown current status.")
        };
    }

    private static QueueStateTransitionResult ValidateFromPlanned(QueueJobStatus nextStatus)
    {
        return nextStatus is QueueJobStatus.Running or QueueJobStatus.Cancelled or QueueJobStatus.Skipped
            ? Allowed("Planned job can start, cancel, or skip.")
            : Rejected("Planned job can only start, cancel, or skip.");
    }

    private static QueueStateTransitionResult ValidateFromWaitingForReview(QueueJobStatus nextStatus)
    {
        return nextStatus is QueueJobStatus.Planned or QueueJobStatus.WaitingForDependencies or QueueJobStatus.Cancelled
            ? Allowed("Reviewed job can become planned, wait on dependencies, or cancel.")
            : Rejected("Review-required job must be reviewed or cancelled before execution.");
    }

    private static QueueStateTransitionResult ValidateFromWaitingForDependencies(QueueJobStatus nextStatus)
    {
        return nextStatus is QueueJobStatus.Planned or QueueJobStatus.Cancelled or QueueJobStatus.Skipped
            ? Allowed("Dependency-blocked job can unblock, cancel, or skip.")
            : Rejected("Dependency-blocked job cannot run until blockers clear.");
    }

    private static QueueStateTransitionResult ValidateFromRunning(QueueJobStatus nextStatus)
    {
        return nextStatus is QueueJobStatus.Succeeded or QueueJobStatus.Failed or QueueJobStatus.Cancelled
            ? Allowed("Running job can finish, fail, or cancel.")
            : Rejected("Running job can only finish, fail, or cancel.");
    }

    private static QueueStateTransitionResult Allowed(string reason)
    {
        return new QueueStateTransitionResult
        {
            IsAllowed = true,
            Reason = reason
        };
    }

    private static QueueStateTransitionResult Rejected(string reason)
    {
        return new QueueStateTransitionResult
        {
            IsAllowed = false,
            Reason = reason
        };
    }
}
