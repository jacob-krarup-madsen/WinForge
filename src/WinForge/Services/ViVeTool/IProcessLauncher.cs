using System;
using System.Threading;
using System.Threading.Tasks;

namespace ViVeToolApp.Services;

/// <summary>
/// Abstraction for launching operating system processes.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Executes a process asynchronously and captures standard output and standard error.
    /// </summary>
    /// <param name="fileName">The executable file to launch.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing ExitCode, StandardOutput, and StandardError.</returns>
    Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default);
}
