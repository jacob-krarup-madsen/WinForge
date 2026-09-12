using System;
using System.IO;

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

        // 2. Specified or application base directory
        if (!string.IsNullOrWhiteSpace(customBaseDirectory))
        {
            var localCandidate = Path.Combine(customBaseDirectory, "vivetool.exe");
            if (File.Exists(localCandidate))
            {
                return localCandidate;
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

        // 4. Legacy tools folder fallback (only if no explicit custom directory or custom pathEnv was specified)
        if (string.IsNullOrWhiteSpace(customBaseDirectory) && pathEnvironment == null)
        {
            const string legacyCandidate = @"C:\Tools\vivetool_feature_enabler\vivetool.exe";
            if (File.Exists(legacyCandidate))
            {
                return legacyCandidate;
            }
        }

        return null;
    }
}
