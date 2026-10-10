using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Win32-backed <see cref="IMemoryNative"/> using <c>GlobalMemoryStatusEx</c> and <c>EmptyWorkingSet</c>.
/// </summary>
public sealed partial class WindowsMemoryNative : IMemoryNative
{
    private const uint ProcessSetQuota = 0x0100;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <inheritdoc />
    public MemoryInfo QueryMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new InvalidOperationException($"GlobalMemoryStatusEx failed (error {Marshal.GetLastWin32Error()}).");
        }

        return new MemoryInfo(status.TotalPhys, status.AvailPhys);
    }

    /// <inheritdoc />
    public IReadOnlyList<int> GetProcessIds()
    {
        var processes = Process.GetProcesses();
        try
        {
            var ids = new List<int>(processes.Length);
            foreach (var process in processes)
            {
                ids.Add(process.Id);
            }

            return ids;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public bool TryTrimWorkingSet(int processId)
    {
        // PID 0 (Idle) and 4 (System) cannot be opened or trimmed.
        if (processId <= 4)
        {
            return false;
        }

        IntPtr handle = OpenProcess(ProcessSetQuota | ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return EmptyWorkingSet(handle);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr process);
}
