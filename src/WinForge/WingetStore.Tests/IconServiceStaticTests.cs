namespace WingetStore.Tests;

public class IconServiceStaticTests
{
    [Fact]
    public void ParseDatabaseJson_ValidPayload_ParsesIconsAndScreenshots()
    {
        string json = """
        {
          "icons_and_screenshots": {
            "Git.Git": {
              "icon": "https://example.com/git.png",
              "images": [ "https://example.com/shot1.png", "https://example.com/shot2.png" ]
            }
          }
        }
        """;

        var (icons, screenshots) = IconService.ParseDatabaseJson(json);
        Assert.Single(icons);
        Assert.Equal("https://example.com/git.png", icons["Git.Git"]);
        Assert.Single(screenshots);
        Assert.Equal(2, screenshots["Git.Git"].Count);
    }

    [Fact]
    public void ParseDatabaseJson_MissingProperty_ReturnsEmptyDictionaries()
    {
        string json = "{\"other_property\": {}}";
        var (icons, screenshots) = IconService.ParseDatabaseJson(json);
        Assert.Empty(icons);
        Assert.Empty(screenshots);
    }

    [Fact]
    public void ParseDatabaseJson_FiltersEmptyOrNullImageStrings()
    {
        string json = """
        {
          "icons_and_screenshots": {
            "App.Id": {
              "icon": "",
              "images": [ "", "https://example.com/shot1.png" ]
            }
          }
        }
        """;

        var (icons, screenshots) = IconService.ParseDatabaseJson(json);
        Assert.Empty(icons);
        Assert.Single(screenshots);
        Assert.Single(screenshots["App.Id"]);
    }

    [Fact]
    public void ParseDatabaseJson_MalformedJson_ReturnsEmptyDictionariesWithoutThrowing()
    {
        var (icons, screenshots) = IconService.ParseDatabaseJson("{ invalid json ");
        Assert.Empty(icons);
        Assert.Empty(screenshots);
    }

    [Fact]
    public void ParseDatabaseJson_CaseInsensitiveKeys()
    {
        string json = """
        {
          "icons_and_screenshots": {
            "Git.Git": { "icon": "https://example.com/git.png" }
          }
        }
        """;

        var (icons, _) = IconService.ParseDatabaseJson(json);
        Assert.True(icons.ContainsKey("git.git"));
    }

    [Fact]
    public void IsCacheExpired_WithinThreshold_ReturnsFalse()
    {
        DateTime now = DateTime.Now;
        DateTime lastWrite = now.AddHours(-23);
        Assert.False(IconService.IsCacheExpired(lastWrite, now, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void IsCacheExpired_ExceedsThreshold_ReturnsTrue()
    {
        DateTime now = DateTime.Now;
        DateTime lastWrite = now.AddHours(-25);
        Assert.True(IconService.IsCacheExpired(lastWrite, now, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void IsCacheExpired_FutureTimestamp_ReturnsTrue()
    {
        DateTime now = DateTime.Now;
        DateTime lastWrite = now.AddHours(2);
        Assert.True(IconService.IsCacheExpired(lastWrite, now, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ExtractHomepageFromShowOutput_ValidOutput_ReturnsHomepageUrl()
    {
        string showOutput = "Publisher: Microsoft\r\nHomepage: https://microsoft.com\r\nLicense: MIT";
        string homepage = IconService.ExtractHomepageFromShowOutput(showOutput);
        Assert.Equal("https://microsoft.com", homepage);
    }

    [Fact]
    public void ExtractHomepageFromShowOutput_NoHomepage_ReturnsEmptyString()
    {
        string showOutput = "Publisher: Microsoft\r\nLicense: MIT";
        Assert.Equal("", IconService.ExtractHomepageFromShowOutput(showOutput));
    }

    [Fact]
    public void ExtractHomepageFromShowOutput_NullOrEmptyOutput_ReturnsEmptyString()
    {
        Assert.Equal("", IconService.ExtractHomepageFromShowOutput(""));
        Assert.Equal("", IconService.ExtractHomepageFromShowOutput(null!));
    }

    [Fact]
    public void ExtractDomainFromUrl_StandardUrl_ReturnsHost()
    {
        Assert.Equal("github.com", IconService.ExtractDomainFromUrl("https://github.com/microsoft/winget-cli"));
    }

    [Fact]
    public void ExtractDomainFromUrl_StripsWwwPrefix()
    {
        Assert.Equal("google.com", IconService.ExtractDomainFromUrl("https://www.google.com/search"));
    }

    [Fact]
    public void ExtractDomainFromUrl_InvalidUrl_ReturnsEmptyString()
    {
        Assert.Equal("", IconService.ExtractDomainFromUrl("not a url"));
        Assert.Equal("", IconService.ExtractDomainFromUrl(""));
    }

    [Fact]
    public void GetHunterLogoUrl_ValidDomain_ReturnsFormattedUrl()
    {
        Assert.Equal("https://logos.hunter.io/example.com", IconService.GetHunterLogoUrl("example.com"));
    }

    [Fact]
    public void GetHunterLogoUrl_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.Equal("", IconService.GetHunterLogoUrl(""));
        Assert.Equal("", IconService.GetHunterLogoUrl(null!));
    }

    [Fact]
    public void GetGoogleFaviconUrl_ValidDomain_ReturnsFormattedUrl()
    {
        Assert.Equal("https://www.google.com/s2/favicons?domain=example.com&sz=128", IconService.GetGoogleFaviconUrl("example.com"));
        Assert.Equal("https://www.google.com/s2/favicons?domain=example.com&sz=64", IconService.GetGoogleFaviconUrl("example.com", 64));
    }

    [Fact]
    public void GetGoogleFaviconUrl_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.Equal("", IconService.GetGoogleFaviconUrl(""));
    }

    [Fact]
    public void GetIconUrlCandidates_ValidDomain_HunterFirstThenFaviconFallback()
    {
        string[] candidates = IconService.GetIconUrlCandidates("example.com").ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.Equal("https://logos.hunter.io/example.com", candidates[0]);
        Assert.Equal("https://www.google.com/s2/favicons?domain=example.com&sz=128", candidates[1]);
    }

    [Fact]
    public void GetIconUrlCandidates_EmptyOrNullDomain_ReturnsNoCandidates()
    {
        Assert.Empty(IconService.GetIconUrlCandidates(""));
        Assert.Empty(IconService.GetIconUrlCandidates(null!));
    }

    [Fact]
    public void GetIconUrlCandidates_DeferredEnumeration_MatchesGetHunterAndFaviconUrls()
    {
        var candidates = IconService.GetIconUrlCandidates("github.com").ToArray();
        Assert.Equal(IconService.GetHunterLogoUrl("github.com"), candidates[0]);
        Assert.Equal(IconService.GetGoogleFaviconUrl("github.com"), candidates[1]);
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/jpg", true)]
    [InlineData("image/gif", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/x-icon", true)]
    [InlineData("image/vnd.microsoft.icon", true)]
    [InlineData("image/bmp", true)]
    [InlineData("image/svg+xml", true)]
    [InlineData("application/octet-stream", true)]
    [InlineData("image/png; charset=utf-8", true)]
    [InlineData("text/html", false)]
    [InlineData("application/javascript", false)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedContentType_ValidatesContentTypeHeaders(string? contentType, bool expected)
    {
        bool actual = IconService.IsAllowedContentType(contentType);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsValidImageHeader_PNG_ReturnsTrue()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(IconService.IsValidImageHeader(pngHeader, pngHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_JPEG_ReturnsTrue()
    {
        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        Assert.True(IconService.IsValidImageHeader(jpegHeader, jpegHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_GIF_ReturnsTrue()
    {
        byte[] gifHeader = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a'];
        Assert.True(IconService.IsValidImageHeader(gifHeader, gifHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_BMP_ReturnsTrue()
    {
        byte[] bmpHeader = [(byte)'B', (byte)'M', 0x00, 0x00];
        Assert.True(IconService.IsValidImageHeader(bmpHeader, bmpHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_ICO_ReturnsTrue()
    {
        byte[] icoHeader = [0x00, 0x00, 0x01, 0x00, 0x01, 0x00];
        Assert.True(IconService.IsValidImageHeader(icoHeader, icoHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_WEBP_ReturnsTrue()
    {
        byte[] webpHeader = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x00, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];
        Assert.True(IconService.IsValidImageHeader(webpHeader, webpHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_SVG_ReturnsTrue()
    {
        byte[] svgHeader = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        Assert.True(IconService.IsValidImageHeader(svgHeader, svgHeader.Length));
    }

    [Fact]
    public void IsValidImageHeader_ExecutablesAndHtml_ReturnsFalse()
    {
        byte[] exeHeader = [(byte)'M', (byte)'Z', 0x90, 0x00];
        byte[] htmlHeader = System.Text.Encoding.UTF8.GetBytes("<html><body>Malicious HTML</body></html>");
        byte[] scriptHeader = System.Text.Encoding.UTF8.GetBytes("console.log('malicious script');");
        byte[] nullHeader = [];

        Assert.False(IconService.IsValidImageHeader(exeHeader, exeHeader.Length));
        Assert.False(IconService.IsValidImageHeader(htmlHeader, htmlHeader.Length));
        Assert.False(IconService.IsValidImageHeader(scriptHeader, scriptHeader.Length));
        Assert.False(IconService.IsValidImageHeader(nullHeader, 0));
        Assert.False(IconService.IsValidImageHeader(null!, 0));
    }

    [Fact]
    public void MaxIconSizeBytes_Enforces5MB()
    {
        Assert.Equal(5 * 1024 * 1024, IconService.MaxIconSizeBytes);
    }

    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/pjpeg", ".jpg")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/x-icon", ".ico")]
    [InlineData("image/vnd.microsoft.icon", ".ico")]
    [InlineData("image/ico", ".ico")]
    [InlineData("image/icon", ".ico")]
    [InlineData("image/bmp", ".bmp")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("application/octet-stream", ".png")]
    [InlineData("image/png; charset=utf-8", ".png")]
    [InlineData("image/webp; charset=binary", ".webp")]
    [InlineData("IMAGE/JPEG", ".jpg")]
    [InlineData("", ".png")]
    [InlineData(null, ".png")]
    [InlineData("text/html", ".png")]
    [InlineData("image/avif", ".png")]
    public void GetFileExtensionForContentType_MapsAllowedContentTypes(string? mediaType, string expected)
    {
        Assert.Equal(expected, IconService.GetFileExtensionForContentType(mediaType));
    }

    [Fact]
    public void GetSafeIconFileName_WithExtension_UsesProvidedExtension()
    {
        Assert.Equal("App.svg", IconService.GetSafeIconFileName("App", ".svg"));
        Assert.Equal("App_Name.webp", IconService.GetSafeIconFileName("App:Name", ".webp"));
        Assert.Equal("App.Name.jpg", IconService.GetSafeIconFileName("App.Name", ".jpg"));
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName("", ".png"));
        Assert.Equal("unknown.gif", IconService.GetSafeIconFileName(null!, ".gif"));
    }

    [Fact]
    public void FindLocalIconFilePath_FindsFileWithContentTypeExtension()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "wingetstore-icon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Git.Git.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            string? found = IconService.FindLocalIconFilePath(tempDir, "Git.Git");
            Assert.Equal(Path.Combine(tempDir, "Git.Git.svg"), found);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindLocalIconFilePath_NoMatchingFile_ReturnsNull()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "wingetstore-icon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.Null(IconService.FindLocalIconFilePath(tempDir, "No.Such.Package"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindLocalIconFilePath_NullOrEmptyPackageId_ReturnsNull()
    {
        string tempDir = Path.GetTempPath();
        Assert.Null(IconService.FindLocalIconFilePath(tempDir, null!));
        Assert.Null(IconService.FindLocalIconFilePath(tempDir, ""));
    }

    [Fact]
    public void FindLocalIconFilePath_SanitizesPackageId()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "wingetstore-icon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "App_Name.png"), "png");
            string? found = IconService.FindLocalIconFilePath(tempDir, "App:Name");
            Assert.Equal(Path.Combine(tempDir, "App_Name.png"), found);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
