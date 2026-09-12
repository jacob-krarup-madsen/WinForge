namespace WingetStore.Tests;

public class NoWingetPageTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(150, 100, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(75, 200, 37.5)]
    [InlineData(0, -1, 0)]
    public void CalculateDownloadProgress_ReturnsExpected(long totalRead, long totalBytes, double expected)
    {
        double result = NoWingetPage.CalculateDownloadProgress(totalRead, totalBytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTempInstallerPath_ReturnsExpectedPath()
    {
        string tempDir = @"C:\Temp\WingetStore";
        string result = NoWingetPage.GetTempInstallerPath(tempDir);
        Assert.Equal(@"C:\Temp\WingetStore\Microsoft.DesktopAppInstaller.msixbundle", result);
    }

    [Fact]
    public void GetPowershellInstallArguments_ReturnsExpectedArguments()
    {
        string tempPath = @"C:\Temp\installer.msixbundle";
        string result = NoWingetPage.GetPowershellInstallArguments(tempPath);
        Assert.Equal("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path 'C:\\Temp\\installer.msixbundle'\"", result);
    }

    [Fact]
    public void VerifyFileHash_CorrectHash_ReturnsTrue()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            byte[] content = System.Text.Encoding.UTF8.GetBytes("test content for hashing");
            File.WriteAllBytes(tempFile, content);
            string expectedHash = NoWingetPage.ComputeFileHash(tempFile);
            Assert.True(NoWingetPage.VerifyFileHash(tempFile, expectedHash));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void VerifyFileHash_WrongHash_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "some data");
            Assert.False(NoWingetPage.VerifyFileHash(tempFile, "0000000000000000000000000000000000000000000000000000000000000000"));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void VerifyFileHash_NonExistentFile_ReturnsFalse()
    {
        Assert.False(NoWingetPage.VerifyFileHash(@"C:\nonexistent\file.bin", "ABCDEF1234567890"));
    }

    [Theory]
    [InlineData(null, "somehash")]
    [InlineData("", "somehash")]
    [InlineData("   ", "somehash")]
    public void VerifyFileHash_NullOrEmptyFilePath_ReturnsFalse(string? filePath, string hash)
    {
        Assert.False(NoWingetPage.VerifyFileHash(filePath!, hash));
    }

    [Fact]
    public void VerifyFileHash_NullOrEmptyHash_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");
            Assert.False(NoWingetPage.VerifyFileHash(tempFile, null!));
            Assert.False(NoWingetPage.VerifyFileHash(tempFile, ""));
            Assert.False(NoWingetPage.VerifyFileHash(tempFile, "   "));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void ComputeFileHash_ValidFile_ReturnsNonEmptyHexString()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "hello world");
            string hash = NoWingetPage.ComputeFileHash(tempFile);
            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void ComputeFileHash_NonExistentFile_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NoWingetPage.ComputeFileHash(@"C:\nonexistent\file.bin"));
    }

    [Fact]
    public void VerifyFileHash_CaseInsensitive_ReturnsTrue()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "case test");
            string hash = NoWingetPage.ComputeFileHash(tempFile);
            Assert.True(NoWingetPage.VerifyFileHash(tempFile, hash.ToLowerInvariant()));
            Assert.True(NoWingetPage.VerifyFileHash(tempFile, hash.ToUpperInvariant()));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void VerifyFileHash_EmptyFile_MatchesEmptyContentHash()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            byte[] emptyHash = Convert.FromHexString("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
            string expected = Convert.ToHexString(emptyHash);
            Assert.True(NoWingetPage.VerifyFileHash(tempFile, expected));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void IsInstallerFileValid_MatchingHash_ReturnsTrue()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "installer payload");
            string hash = NoWingetPage.ComputeFileHash(tempFile);
            Assert.True(NoWingetPage.IsInstallerFileValid(tempFile, hash));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void IsInstallerFileValid_WrongHash_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "tampered payload");
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, "0000000000000000000000000000000000000000000000000000000000000000"));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void IsInstallerFileValid_EmptyFile_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            string emptyContentHash = Convert.ToHexString(Convert.FromHexString("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, emptyContentHash));
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, NoWingetPage.WingetInstallerSha256));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void IsInstallerFileValid_NonExistentFile_ReturnsFalse()
    {
        Assert.False(NoWingetPage.IsInstallerFileValid(@"C:\nonexistent\file.bin", NoWingetPage.WingetInstallerSha256));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInstallerFileValid_NullOrEmptyFilePath_ReturnsFalse(string? filePath)
    {
        Assert.False(NoWingetPage.IsInstallerFileValid(filePath!, NoWingetPage.WingetInstallerSha256));
    }

    [Fact]
    public void IsInstallerFileValid_NullOrEmptyHash_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, null!));
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, ""));
            Assert.False(NoWingetPage.IsInstallerFileValid(tempFile, "   "));
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void WingetInstallerSha256_Is64HexCharsAndMatchesPinnedRelease()
    {
        Assert.Equal(64, NoWingetPage.WingetInstallerSha256.Length);
        Assert.True(NoWingetPage.WingetInstallerSha256.All(Uri.IsHexDigit));
        Assert.Equal("0809FA9F52E395D6E7DE692331DCE847AC991952675116BB4D8AAE2DDCC20946", NoWingetPage.WingetInstallerSha256);
    }

    [Theory]
    [InlineData(NoWingetPage.InstallStep.Downloading, "Downloading Winget installer...")]
    [InlineData(NoWingetPage.InstallStep.Verifying, "Verifying installer integrity...")]
    [InlineData(NoWingetPage.InstallStep.Installing, "Installing Winget...")]
    [InlineData(NoWingetPage.InstallStep.Success, "Installation successful! Starting application...")]
    [InlineData(NoWingetPage.InstallStep.LaunchingGui, "Launching App Installer GUI...")]
    [InlineData(NoWingetPage.InstallStep.Cancelled, "Installation cancelled.")]
    [InlineData(NoWingetPage.InstallStep.Failed, "Installation failed.")]
    public void GetInstallStatusMessage_ReturnsExpectedMessages(NoWingetPage.InstallStep step, string expected)
    {
        Assert.Equal(expected, NoWingetPage.GetInstallStatusMessage(step));
    }

    [Fact]
    public void GetInstallStatusMessage_FailedWithDetail_ReturnsFailedPrefix()
    {
        Assert.Equal("Failed: network unreachable", NoWingetPage.GetInstallStatusMessage(NoWingetPage.InstallStep.Failed, "network unreachable"));
    }

    [Fact]
    public void GetInstallStatusMessage_NonFailedStepWithDetail_IgnoresDetail()
    {
        Assert.Equal("Downloading Winget installer...", NoWingetPage.GetInstallStatusMessage(NoWingetPage.InstallStep.Downloading, "ignored"));
    }

    [Fact]
    public void GetInstallStatusMessage_UnknownStep_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NoWingetPage.GetInstallStatusMessage((NoWingetPage.InstallStep)999));
    }

    [Theory]
    [InlineData("https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle", true)]
    [InlineData("https://www.github.com/microsoft/winget-cli/releases/download/v1.9.0/App.msixbundle", true)]
    [InlineData("HTTPS://GITHUB.COM/MICROSOFT/WINGET-CLI/RELEASES/LATEST/DOWNLOAD/APP.MSIXBUNDLE", true)]
    public void IsTrustedWingetDownloadUrl_TrustedUrls_ReturnsTrue(string url, bool expected)
    {
        Assert.Equal(expected, NoWingetPage.IsTrustedWingetDownloadUrl(url));
    }

    [Theory]
    [InlineData("https://evil.com/microsoft/winget-cli/download/Microsoft.DesktopAppInstaller.msixbundle")]
    [InlineData("https://github.com.evil.com/file.msixbundle")]
    [InlineData("https://evilgithub.com/file.msixbundle")]
    [InlineData("https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller.msi")]
    [InlineData("https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller.msix")]
    [InlineData("https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller.exe")]
    [InlineData("https://github.com/microsoft/winget-cli/releases")]
    [InlineData("https://github.com/microsoft/winget-cli/releases/latest/download/")]
    [InlineData("not a url.msixbundle")]
    [InlineData("")]
    [InlineData(null)]
    public void IsTrustedWingetDownloadUrl_UntrustedUrls_ReturnsFalse(string? url)
    {
        Assert.False(NoWingetPage.IsTrustedWingetDownloadUrl(url!));
    }
}
