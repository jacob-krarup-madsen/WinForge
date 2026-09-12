using System.Diagnostics;
using System.Text;

namespace WingetStore.Services;

public class CliProcessRunner : IProcessRunner
{
    public async Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo { FileName = fileName, Arguments = arguments, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (s, e) => { if (e.Data != null) onLineReceived(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine($"[winget stderr] {e.Data}"); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using (cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception ex) { Debug.WriteLine($"Failed to kill process tree: {ex.Message}"); } }))
        {
            try { await process.WaitForExitAsync(cancellationToken); }
            catch (OperationCanceledException) { try { process.WaitForExit(500); } catch { } throw; }
            try { process.WaitForExit(1000); } catch { }
            return process.ExitCode;
        }
    }
}
