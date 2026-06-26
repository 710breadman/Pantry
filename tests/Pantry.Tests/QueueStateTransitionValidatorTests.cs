using Pantry.Queue;

namespace Pantry.Tests;

public sealed class QueueStateTransitionValidatorTests
{
    [Theory]
    [InlineData(QueueJobStatus.Planned, QueueJobStatus.Running)]
    [InlineData(QueueJobStatus.Planned, QueueJobStatus.Cancelled)]
    [InlineData(QueueJobStatus.WaitingForReview, QueueJobStatus.Planned)]
    [InlineData(QueueJobStatus.WaitingForReview, QueueJobStatus.WaitingForDependencies)]
    [InlineData(QueueJobStatus.WaitingForDependencies, QueueJobStatus.Planned)]
    [InlineData(QueueJobStatus.Running, QueueJobStatus.Succeeded)]
    [InlineData(QueueJobStatus.Running, QueueJobStatus.Failed)]
    public void Allows_safe_transitions(QueueJobStatus currentStatus, QueueJobStatus nextStatus)
    {
        var validator = new QueueStateTransitionValidator();

        var result = validator.Validate(currentStatus, nextStatus);

        Assert.True(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(QueueJobStatus.WaitingForReview, QueueJobStatus.Running)]
    [InlineData(QueueJobStatus.WaitingForDependencies, QueueJobStatus.Running)]
    [InlineData(QueueJobStatus.Running, QueueJobStatus.Planned)]
    [InlineData(QueueJobStatus.Succeeded, QueueJobStatus.Running)]
    [InlineData(QueueJobStatus.Failed, QueueJobStatus.Running)]
    [InlineData(QueueJobStatus.Cancelled, QueueJobStatus.Running)]
    public void Rejects_unsafe_transitions(QueueJobStatus currentStatus, QueueJobStatus nextStatus)
    {
        var validator = new QueueStateTransitionValidator();

        var result = validator.Validate(currentStatus, nextStatus);

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
