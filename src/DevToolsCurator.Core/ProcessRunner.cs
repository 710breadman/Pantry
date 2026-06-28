using System.Diagnostics;
using System.Text;

namespace DevToolsCurator.Core;

public sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}

public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
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

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

            if (!process.Start())
            {
                return new ProcessResult(127, "", "Process failed to start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delayTask = Task.Delay(timeout, timeoutCts.Token);
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(exitTask, delayTask);
            if (completed == delayTask)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new ProcessResult(124, output.ToString(), "Timed out.");
            }

            timeoutCts.Cancel();
            await exitTask;
            return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
        }
        catch (Exception ex)
        {
            return new ProcessResult(127, "", ex.Message);
        }
    }
}
