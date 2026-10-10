using System;
using System.Security.Principal;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Reports whether the current process runs with administrative privileges.
/// </summary>
public interface IElevationService
{
    /// <summary>Gets a value indicating whether the process is elevated.</summary>
    bool IsElevated { get; }
}

/// <summary>
/// Windows implementation that inspects the current process token.
/// </summary>
public sealed class WindowsElevationService : IElevationService
{
    /// <inheritdoc />
    public bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or InvalidOperationException)
            {
                return false;
            }
        }
    }
}
