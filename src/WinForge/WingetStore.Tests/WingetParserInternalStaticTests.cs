namespace WingetStore.Tests;

public class WingetParserInternalStaticTests
{
    [Fact]
    public void FindHeaderLine_WithValidSeparator_ReturnsHeaderIndex()
    {
        string[] lines = ["Name Id Version", "--- -- -------", "App1 1.0 1.0"];
        Assert.Equal(0, WingetParser.FindHeaderLine(lines));
    }

    [Fact]
    public void FindHeaderLine_NoSeparator_ReturnsNegativeOne()
    {
        string[] lines = ["Name Id Version", "App1 1.0 1.0"];
        Assert.Equal(-1, WingetParser.FindHeaderLine(lines));
    }

    [Fact]
    public void FindHeaderLine_SeparatorAtFirstLine_ReturnsNegativeOne()
    {
        string[] lines = ["---", "App1 1.0"];
        Assert.Equal(-1, WingetParser.FindHeaderLine(lines));
    }

    [Fact]
    public void FindHeaderLine_EmptyArray_ReturnsNegativeOne()
    {
        Assert.Equal(-1, WingetParser.FindHeaderLine([]));
    }

    [Fact]
    public void TryParseColumnPositions_StandardHeader_ReturnsPositions()
    {
        string headerLine = "Name Id Version Source";
        bool success = WingetParser.TryParseColumnPositions(headerLine, out var pos);
        Assert.True(success);
        Assert.Equal(0, pos.namePos);
        Assert.Equal(5, pos.idPos);
        Assert.Equal(8, pos.versionPos);
        Assert.Equal(16, pos.sourcePos);
    }

    [Fact]
    public void TryParseColumnPositions_UpgradeHeader_ReturnsAvailablePosition()
    {
        string headerLine = "Name Id Version Available Source";
        bool success = WingetParser.TryParseColumnPositions(headerLine, out var pos);
        Assert.True(success);
        Assert.Equal(16, pos.availablePos);
    }

    [Fact]
    public void TryParseColumnPositions_MatchHeader_ReturnsMatchPosition()
    {
        string headerLine = "Name Id Version Match";
        bool success = WingetParser.TryParseColumnPositions(headerLine, out var pos);
        Assert.True(success);
        Assert.Equal(16, pos.matchPos);
    }

    [Fact]
    public void TryParseColumnPositions_MissingIdOrVersion_ReturnsFalse()
    {
        Assert.False(WingetParser.TryParseColumnPositions("Name Version Source", out _));
        Assert.False(WingetParser.TryParseColumnPositions("Name Id Source", out _));
    }

    [Fact]
    public void TryParseColumnPositions_InvalidColumnOrder_ReturnsFalse()
    {
        Assert.False(WingetParser.TryParseColumnPositions("Version Id Name", out _));
    }

    [Fact]
    public void ParseTableRow_StandardRow_PopulatesDictionary()
    {
        (int namePos, int idPos, int versionPos, int sourcePos, int matchPos, int availablePos) pos = (0, 10, 20, 30, -1, -1);
        string line = "TestApp   App.Id    1.0.0     winget";
        var dict = WingetParser.ParseTableRow(line, pos);
        Assert.Equal("TestApp", dict["Name"]);
        Assert.Equal("App.Id", dict["Id"]);
        Assert.Equal("1.0.0", dict["Version"]);
        Assert.Equal("winget", dict["Source"]);
    }

    [Fact]
    public void ParseTableRow_AvailableColumn_PopulatesAvailableKey()
    {
        (int namePos, int idPos, int versionPos, int sourcePos, int matchPos, int availablePos) pos = (0, 10, 20, -1, -1, 30);
        string line = "TestApp   App.Id    1.0.0     2.0.0";
        var dict = WingetParser.ParseTableRow(line, pos);
        Assert.Equal("2.0.0", dict["Available"]);
    }

    [Fact]
    public void ParseTableRow_MatchColumn_PopulatesMatchKey()
    {
        (int namePos, int idPos, int versionPos, int sourcePos, int matchPos, int availablePos) pos = (0, 10, 20, -1, 30, -1);
        string line = "TestApp   App.Id    1.0.0     Tag:Test";
        var dict = WingetParser.ParseTableRow(line, pos);
        Assert.Equal("Tag:Test", dict["Match"]);
    }

    [Fact]
    public void TryParseFoundLine_ValidFoundHeader_SetsPackageNameAndReturnsTrue()
    {
        var pkg = new WingetPackage();
        bool result = WingetParser.TryParseFoundLine("Found Git [Git.Git]", pkg);
        Assert.True(result);
        Assert.Equal("Git", pkg.Name);
    }

    [Fact]
    public void TryParseFoundLine_FoundLineWithoutBracket_ReturnsTrueWithoutSettingName()
    {
        var pkg = new WingetPackage();
        bool result = WingetParser.TryParseFoundLine("Found Git", pkg);
        Assert.True(result);
        Assert.Empty(pkg.Name);
    }

    [Fact]
    public void TryParseFoundLine_NonFoundLine_ReturnsFalse()
    {
        var pkg = new WingetPackage();
        bool result = WingetParser.TryParseFoundLine("Publisher: Microsoft", pkg);
        Assert.False(result);
    }

    [Fact]
    public void SetPackageField_ValidMetadataKeys_SetsPropertiesCorrectly()
    {
        var pkg = new WingetPackage();
        WingetParser.SetPackageField(pkg, "Name", "Test");
        WingetParser.SetPackageField(pkg, "Version", "1.2.3");
        WingetParser.SetPackageField(pkg, "Publisher", "Pub");
        WingetParser.SetPackageField(pkg, "Publisher Url", "https://pub.com");
        WingetParser.SetPackageField(pkg, "Description", "Desc");
        WingetParser.SetPackageField(pkg, "Homepage", "https://home.com");
        WingetParser.SetPackageField(pkg, "License", "MIT");
        WingetParser.SetPackageField(pkg, "Release Notes", "Notes");

        Assert.Equal("Test", pkg.Name);
        Assert.Equal("1.2.3", pkg.Version);
        Assert.Equal("Pub", pkg.Publisher);
        Assert.Equal("https://pub.com", pkg.PublisherUrl);
        Assert.Equal("Desc", pkg.Description);
        Assert.Equal("https://home.com", pkg.Homepage);
        Assert.Equal("MIT", pkg.License);
        Assert.Equal("Notes", pkg.ReleaseNotes);
    }

    [Fact]
    public void SetPackageField_UnknownKey_DoesNotThrow()
    {
        var pkg = new WingetPackage();
        WingetParser.SetPackageField(pkg, "UnknownKey", "Val");
        Assert.Empty(pkg.Name);
    }

    [Fact]
    public void IsUrl_HttpAndHttpsUrls_ReturnsTrue()
    {
        Assert.True(WingetParser.IsUrl("http://example.com"));
        Assert.True(WingetParser.IsUrl("https://example.com/path"));
    }

    [Fact]
    public void IsUrl_NonHttpUrlsAndPaths_ReturnsFalse()
    {
        Assert.False(WingetParser.IsUrl("ftp://example.com"));
        Assert.False(WingetParser.IsUrl("C:\\Program Files"));
        Assert.False(WingetParser.IsUrl("invalid_string"));
    }
}
