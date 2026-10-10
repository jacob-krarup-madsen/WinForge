using System;
using System.IO;
using WingetStore.Services;

namespace ViVeToolApp.Services;

/// <summary>
/// Probes file system paths and environment variables to locate the ViVeTool CLI binary.
/// </summary>
public class ViVeToolLocator : IViVeToolLocator
{
    public string? LocateViVeTool(string? customBaseDirectory = null, string? customPath = null, string? pathEnvironment = null)
    {
        // 1. Direct path check
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        // 2. Specified or application base directory (root and Tools subfolder)
        if (!string.IsNullOrWhiteSpace(customBaseDirectory))
        {
            var localCandidate = Path.Combine(customBaseDirectory, "vivetool.exe");
            if (File.Exists(localCandidate))
            {
                return localCandidate;
            }

            var subDirCandidate = Path.Combine(customBaseDirectory, "Tools", "vivetool.exe");
            if (File.Exists(subDirCandidate))
            {
                return subDirCandidate;
            }
        }
        else
        {
            var baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var localCandidate = Path.Combine(baseDir, "vivetool.exe");
            if (File.Exists(localCandidate))
            {
                return localCandidate;
            }

            var subDirCandidate = Path.Combine(baseDir, "Tools", "vivetool.exe");
            if (File.Exists(subDirCandidate))
            {
                return subDirCandidate;
            }
        }

        // 3. System PATH variable
        var pathVar = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, "vivetool.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore invalid PATH entries
            }
        }

        // 4. AppPaths.Root fallback (only if no explicit custom directory or custom pathEnv was specified)
        if (string.IsNullOrWhiteSpace(customBaseDirectory) && pathEnvironment == null)
        {
            var appDataCandidate = Path.Combine(AppPaths.Root, "vivetool.exe");
            if (File.Exists(appDataCandidate))
            {
                return appDataCandidate;
            }
        }

        return null;
    }
}
