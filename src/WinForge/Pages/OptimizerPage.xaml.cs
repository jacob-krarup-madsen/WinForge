using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Pages;

public sealed partial class OptimizerPage : Page
{
    private bool _isRunning;

    public OptimizerPage()
    {
        InitializeComponent();
        Loaded += OptimizerPage_Loaded;
    }

    private void OptimizerPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!IsAdministrator())
        {
            OptInfoBar.Severity = InfoBarSeverity.Warning;
            OptInfoBar.Title = "Elevation Notice";
            OptInfoBar.Message = "WinForge is running without administrative privileges. System optimizations and service modifications require running as Administrator.";
            OptInfoBar.IsOpen = true;
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        OptimizeBtn.IsEnabled = enabled;
        CleanDiskBtn.IsEnabled = enabled;
        FlushMemoryBtn.IsEnabled = enabled;
        AuditBtn.IsEnabled = enabled;
        UndoBtn.IsEnabled = enabled;
    }

    private void OptimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = RunScriptAsync("Optimize-Windows.ps1", "Windows System Optimization");
    }

    private void CleanDiskBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = RunScriptAsync("Clean-Disk.ps1", "Disk & Cache Cleanup");
    }

    private void FlushMemoryBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = RunScriptAsync("Flush-Memory.ps1", "Memory Flush");
    }

    private void AuditBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = RunScriptAsync("Audit-System.ps1", "System Audit");
    }

    private void UndoBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = RunScriptAsync("Undo-Optimization.ps1", "Restore Defaults");
    }

    private static string? LocateScript(string scriptName)
    {
        var baseDir = AppContext.BaseDirectory;
        for (int i = 0; i < 7 && !string.IsNullOrEmpty(baseDir); i++)
        {
            var corePath = Path.Combine(baseDir, "tools", "cli", "optimizer", "Core", scriptName);
            if (File.Exists(corePath)) return corePath;

            var optPath = Path.Combine(baseDir, "tools", "cli", "optimizer", scriptName);
            if (File.Exists(optPath)) return optPath;

            var directCore = Path.Combine(baseDir, "Core", scriptName);
            if (File.Exists(directCore)) return directCore;

            var direct = Path.Combine(baseDir, scriptName);
            if (File.Exists(direct)) return direct;

            var parent = Directory.GetParent(baseDir);
            baseDir = parent?.FullName;
        }
        return null;
    }

    private async Task RunScriptAsync(string scriptName, string taskTitle)
    {
        if (_isRunning) return;

        _isRunning = true;
        SetActionButtonsEnabled(false);
        OptInfoBar.IsOpen = false;
        ConsoleOutputBox.Text = $"Starting {taskTitle} ({scriptName})...\n";

        var scriptPath = LocateScript(scriptName);

        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            OptInfoBar.Severity = InfoBarSeverity.Error;
            OptInfoBar.Title = "Script Not Found";
            OptInfoBar.Message = $"PowerShell script '{scriptName}' could not be located in tools/cli/optimizer/Core.";
            OptInfoBar.IsOpen = true;
            _isRunning = false;
            SetActionButtonsEnabled(true);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                ConsoleOutputBox.Text += stdout;
                if (!string.IsNullOrEmpty(stderr))
                {
                    ConsoleOutputBox.Text += $"\n[Errors/Warnings]:\n{stderr}";
                }

                OptInfoBar.Severity = proc.ExitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                OptInfoBar.Title = $"{taskTitle} Completed";
                OptInfoBar.Message = proc.ExitCode == 0 ? "Task finished successfully." : $"Process exited with code {proc.ExitCode}.";
                OptInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            OptInfoBar.Severity = InfoBarSeverity.Error;
            OptInfoBar.Title = "Execution Error";
            OptInfoBar.Message = ex.Message;
            OptInfoBar.IsOpen = true;
        }
        finally
        {
            _isRunning = false;
            SetActionButtonsEnabled(true);
        }
    }
}
