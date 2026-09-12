using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ViVeToolApp.Services;

/// <summary>
/// Standard operating system process launcher using <see cref="Process"/>.
/// </summary>
public class SystemProcessLauncher : IProcessLauncher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        return await RunProcessAsync(fileName, arguments, DefaultTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {fileName}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort process termination
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"Process '{fileName} {arguments}' exceeded timeout of {timeout.TotalSeconds} seconds.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (process.ExitCode, stdout, stderr);
    }
}
