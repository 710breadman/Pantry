namespace Pantry.Detection;

public sealed record ProcessRunResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public bool TimedOut { get; init; }
}

