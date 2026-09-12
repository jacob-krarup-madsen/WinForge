using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WingetStore.Pages;

public sealed partial class NoWingetPage : Page
{
    // Pinned to a specific release so the downloaded bundle always matches the pinned hash below.
    // Update WingetReleaseVersion AND WingetInstallerSha256 together when upgrading.
    private const string WingetReleaseVersion = "v1.29.280";
    private const string WingetDownloadUrl = "https://github.com/microsoft/winget-cli/releases/download/" + WingetReleaseVersion + "/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";

    /// <summary>
    /// Known-good SHA-256 (hex) of the pinned installer, published alongside the release
    /// as Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.txt.
    /// </summary>
    public const string WingetInstallerSha256 = "0809FA9F52E395D6E7DE692331DCE847AC991952675116BB4D8AAE2DDCC20946";

    private static readonly HttpClient SharedHttpClient = new();
    private CancellationTokenSource? _installCts;
    public NoWingetPage() => InitializeComponent();

    public static double CalculateDownloadProgress(long totalRead, long totalBytes) => totalBytes > 0 ? Math.Min(totalRead * 100.0 / totalBytes, 100) : 0;

    public static string GetTempInstallerPath(string tempDir) => Path.Combine(tempDir, "Microsoft.DesktopAppInstaller.msixbundle");
    public static string GetPowershellInstallArguments(string tempPath) => $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{tempPath}'\"";

    /// <summary>
    /// Computes the SHA256 hash of a file and compares it to the expected hex string.
    /// Returns true if the hashes match (case-insensitive).
    /// </summary>
    public static bool VerifyFileHash(string filePath, string expectedSha256Hex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;
        if (string.IsNullOrWhiteSpace(expectedSha256Hex))
            return false;
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA256.HashData(stream);
        string actualHashHex = Convert.ToHexString(hashBytes);
        return string.Equals(actualHashHex, expectedSha256Hex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Guards the installer before execution: rejects missing/empty files and any file whose
    /// SHA-256 does not match the pinned known-good hash.
    /// </summary>
    public static bool IsInstallerFileValid(string filePath, string expectedSha256Hex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;
        if (new FileInfo(filePath).Length == 0)
            return false;
        return VerifyFileHash(filePath, expectedSha256Hex);
    }

    /// <summary>
    /// Computes the SHA256 hash of a file and returns the hex string.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }

    public enum InstallStep
    {
        Downloading,
        Verifying,
        Installing,
        Success,
        LaunchingGui,
        Cancelled,
        Failed
    }

    internal static string GetInstallStatusMessage(InstallStep step) => step switch
    {
        InstallStep.Downloading => "Downloading Winget installer...",
        InstallStep.Verifying => "Verifying installer integrity...",
        InstallStep.Installing => "Installing Winget...",
        InstallStep.Success => "Installation successful! Starting application...",
        InstallStep.LaunchingGui => "Launching App Installer GUI...",
        InstallStep.Cancelled => "Installation cancelled.",
        InstallStep.Failed => "Installation failed.",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
    };

    internal static string GetInstallStatusMessage(InstallStep step, string detail)
    {
        if (step == InstallStep.Failed)
        {
            return $"Failed: {detail}";
        }
        return GetInstallStatusMessage(step);
    }

    internal static bool IsTrustedWingetDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!url.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _installCts?.Cancel();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        InstallButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        try
        {
            if (!IsTrustedWingetDownloadUrl(WingetDownloadUrl))
            {
                throw new InvalidOperationException("The Winget installer download URL is not a trusted Microsoft release.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "WingetStore");
            Directory.CreateDirectory(tempDir);
            string tempPath = GetTempInstallerPath(tempDir);

            StatusText.Text = GetInstallStatusMessage(InstallStep.Downloading);
            using (var response = await SharedHttpClient.GetAsync(WingetDownloadUrl, HttpCompletionOption.ResponseHeadersRead).WaitAsync(ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var stream = await response.Content.ReadAsStreamAsync().WaitAsync(ct);
                using var fileStream = File.Create(tempPath);
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        DispatcherQueue.TryEnqueue(() => InstallProgress.Value = CalculateDownloadProgress(totalRead, totalBytes));
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            // Verify the downloaded file hash against the pinned known-good hash before execution.
            StatusText.Text = GetInstallStatusMessage(InstallStep.Verifying);
            if (!IsInstallerFileValid(tempPath, WingetInstallerSha256))
            {
                try { File.Delete(tempPath); } catch { }
                throw new InvalidOperationException("Downloaded installer failed integrity verification and was not executed.");
            }

            StatusText.Text = GetInstallStatusMessage(InstallStep.Installing);
            InstallProgress.IsIndeterminate = true;

            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = GetPowershellInstallArguments(tempPath),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    if (process.ExitCode == 0)
                    {
                        StatusText.Text = GetInstallStatusMessage(InstallStep.Success);
                        InstallProgress.IsIndeterminate = false;
                        InstallProgress.Value = 100;
                        await Task.Delay(2000, ct);
                        Frame.Navigate(typeof(HomePage));
                        return;
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            StatusText.Text = GetInstallStatusMessage(InstallStep.LaunchingGui);
            Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
            StatusText.Text = "Please complete the installation in the App Installer window, then restart the application.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = GetInstallStatusMessage(InstallStep.Cancelled);
        }
        catch (Exception ex)
        {
            StatusText.Text = GetInstallStatusMessage(InstallStep.Failed, ex.Message);
            InstallButton.IsEnabled = true;
            InstallProgress.IsIndeterminate = false;
        }
    }
}
