using System.Diagnostics;

namespace Pantry.Detection;

public sealed class WindowsProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new ProcessRunResult
                {
                    ExitCode = -1,
                    StandardOutput = string.Empty,
                    StandardError = "Process did not start."
                };
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(linkedSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linkedSource.Token);

            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);

            return new ProcessRunResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = await outputTask.ConfigureAwait(false),
                StandardError = await errorTask.ConfigureAwait(false)
            };
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ProcessRunResult
            {
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = "Process timed out.",
                TimedOut = true
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult
            {
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

