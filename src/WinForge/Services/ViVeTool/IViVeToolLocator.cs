namespace ViVeToolApp.Services;

/// <summary>
/// Service contract for locating the ViVeTool executable on the system.
/// </summary>
public interface IViVeToolLocator
{
    /// <summary>
    /// Searches for vivetool.exe in candidate directories, legacy paths, and PATH environment.
    /// </summary>
    /// <param name="customBaseDirectory">Optional base directory to probe first.</param>
    /// <param name="customPath">Optional direct file path to verify.</param>
    /// <param name="pathEnvironment">Optional custom PATH string.</param>
    /// <returns>Full path to vivetool.exe if found, otherwise null.</returns>
    string? LocateViVeTool(string? customBaseDirectory = null, string? customPath = null, string? pathEnvironment = null);
}
