namespace WingetStore.Tests;

public class WingetParserTests
{
    [Fact]
    public void ParseProgressFromOutput_Tests()
    {
        Assert.Equal(98.0, WingetParser.ParseProgressFromOutput("██████████████████████████████  98%"));
        Assert.Equal(98.0, WingetParser.ParseProgressFromOutput("98%"));
        Assert.Equal(20.0, WingetParser.ParseProgressFromOutput("Downloading installer..."));
        Assert.Equal(60.0, WingetParser.ParseProgressFromOutput("Verifying installer..."));
        Assert.Equal(80.0, WingetParser.ParseProgressFromOutput("Installing package..."));
        Assert.Equal(0.0, WingetParser.ParseProgressFromOutput("Something random..."));
    }

    [Fact]
    public void ParseStatusTextFromOutput_Tests()
    {
        Assert.Equal("Downloading installer...", WingetParser.ParseStatusTextFromOutput("Downloading..."));
        Assert.Equal("Verifying installer...", WingetParser.ParseStatusTextFromOutput("Successfully verified installer hash"));
        Assert.Equal("Installing...", WingetParser.ParseStatusTextFromOutput("Starting package install"));
        Assert.Equal("Completed", WingetParser.ParseStatusTextFromOutput("Successfully installed"));
        Assert.Equal("Uninstalled", WingetParser.ParseStatusTextFromOutput("Successfully uninstalled"));
        Assert.Equal(string.Empty, WingetParser.ParseStatusTextFromOutput("98%"));
        Assert.Equal("This is a very long line that should ...", WingetParser.ParseStatusTextFromOutput("This is a very long line that should be truncated to fit"));
        Assert.Equal("Short line", WingetParser.ParseStatusTextFromOutput("Short line"));
    }

    [Fact]
    public void ParseTable_HeaderAndColumnPermutations()
    {
        // Short output
        Assert.Empty(WingetParser.ParseTable("Only one line"));

        // No separator line
        string noSep = "Name  Id  Version\nGit   Git 2.0.0\nVSCode VS 1.0";
        Assert.Empty(WingetParser.ParseTable(noSep));

        // Missing ID or Version
        string noId = "Name  Version\n----\nGit  2.0.0";
        Assert.Empty(WingetParser.ParseTable(noId));

        // Match column
        string matchTable = "Name  Id  Version  Match\n------------------------\nGit   Git 2.0      git";
        var resultMatch = WingetParser.ParseTable(matchTable);
        Assert.Single(resultMatch);
        Assert.Equal("git", resultMatch[0]["Match"]);

        // Available column
        string availTable = "Name  Id  Version  Available\n----------------------------\nGit   Git 2.0      2.1";
        var resultAvail = WingetParser.ParseTable(availTable);
        Assert.Single(resultAvail);
        Assert.Equal("2.1", resultAvail[0]["Available"]);

        // Default simple columns
        string simpleTable = "Name  Id  Version\n-----------------\nGit   Git 2.0";
        var resultSimple = WingetParser.ParseTable(simpleTable);
        Assert.Single(resultSimple);
        Assert.Equal("2.0", resultSimple[0]["Version"]);
    }

    [Fact]
    public void ParseDetailsList_FilteringAndARP()
    {
        string raw = @"
(1/2) Git [Git.Git]
  Publisher: Software Corp
  Version: 2.40.0
  Origin Source: winget

(2/2) Filtered App [ARP\Filtered]
  Publisher: Bad Publisher
  Version: 1.0.0
";
        var list = WingetParser.ParseDetailsList(raw);
        Assert.Single(list);
        Assert.Equal("Git.Git", list[0].Id);
        Assert.Equal("Software Corp", list[0].Publisher);
        Assert.Equal("2.40.0", list[0].Version);
        Assert.Equal("winget", list[0].Source);
    }

    [Fact]
    public void ParsePackageDetails_Comprehensive()
    {
        string rawDetails = @"
Found Git [Git.Git]
Version: 2.41.0
Publisher: Software Corp
Publisher Url: https://pub.com
Description: A test description
Homepage: http://homepage.org
License: MIT
Release Notes: https://rel.com/notes
Tags:
  git
  vcs
Installer:
  Installer Type: Nullsoft
  Installer Url: https://dl.com/git.exe
  Installer Alt: http://dl.com/git.exe
NoColonRoot
";
        var pkg = WingetParser.ParsePackageDetails(rawDetails, "Git.Git");
        Assert.Equal("Git", pkg.Name);
        Assert.Equal("2.41.0", pkg.Version);
        Assert.Equal("Software Corp", pkg.Publisher);
        Assert.Equal("https://pub.com", pkg.PublisherUrl);
        Assert.Equal("A test description", pkg.Description);
        Assert.Equal("http://homepage.org", pkg.Homepage);
        Assert.Equal("MIT", pkg.License);
        Assert.Equal("https://rel.com/notes", pkg.ReleaseNotes);
        Assert.Equal("Nullsoft", pkg.InstallerType);
        Assert.Equal("https://dl.com/git.exe", pkg.InstallerUrl);
        Assert.Contains("git", pkg.Tags);
        Assert.Contains("vcs", pkg.Tags);

        // Verify details collections are populated correctly
        Assert.NotEmpty(pkg.Details);
        var installerMetadata = pkg.Details.Find(m => m.Key == "Installer");
        Assert.NotNull(installerMetadata);
        Assert.Equal(3, installerMetadata.SubItems.Count);

        var noColonRootMetadata = pkg.Details.Find(m => m.Key == "NoColonRoot");
        Assert.NotNull(noColonRootMetadata);
    }

    [Fact]
    public void ParseTagsFromShowOutput_Tests()
    {
        string rawShow = @"
Name: Git
Version: 2.41.0
Tags:
  git
  vcs
Publisher: Software Corp
";
        var tags = WingetParser.ParseTagsFromShowOutput(rawShow);
        Assert.Equal(2, tags.Count);
        Assert.Contains("git", tags);
        Assert.Contains("vcs", tags);
    }

    [Fact]
    public void WingetParser_AdditionalEdgeCases_Coverage()
    {
        // 1. ParseTable exception path, 2 lines limit, short row substring, upgrades available text
        string exceptionTable = "Name  Source  Version  Id\n------------------------\nGit   Source  2.0      Git";
        Assert.Empty(WingetParser.ParseTable(exceptionTable));
        Assert.Empty(WingetParser.ParseTable("Line1\nLine2"));

        string shortRowTable = "Name      Id        Version   Match\n----------------------------------\nGit       GitID     2.0";
        Assert.Single(WingetParser.ParseTable(shortRowTable));

        string upgradesTextTable = "Name  Id  Version\n-----------------\nGit   Git 2.0\nupgrades available\nupgrade available";
        Assert.Single(WingetParser.ParseTable(upgradesTextTable));

        // 2. ParsePackageDetails edge cases
        // - Starts with "Found " but no bracket
        // - indent >= 2 but currentParent is null
        // - root key that is not in switch, has http URL
        // - root key that has no colon
        // - Name: root key switch case
        // - custom subkey with colon under Installer
        // - custom subkey without colon under NoColonRoot
        string rawDetails = @"
Found Git Without Bracket
  SubKey: SubVal
CustomKey: http://custom.com
NoColonRoot
  CustomNoColonSubKey
Installer:
  Installer SHA256: 123
  EmptyVal:
";
        var pkg = WingetParser.ParsePackageDetails(rawDetails, "Git.Git");
        Assert.Equal("", pkg.Name); // "Found Git Without Bracket" was skipped because of no bracket
        Assert.Equal("Git.Git", pkg.Id);
        var customMeta = pkg.Details.Find(m => m.Key == "CustomKey");
        Assert.NotNull(customMeta);
        Assert.True(customMeta.IsUrl);
        Assert.Equal("http://custom.com", customMeta.Value);

        var pkgNameSwitch = WingetParser.ParsePackageDetails("Name: GitAppName", "Git.Git");
        Assert.Equal("GitAppName", pkgNameSwitch.Name);

        // 3. ParseTagsFromShowOutput edge cases
        // - tab indentation
        // - empty tag lines
        string rawShow = "Tags:\n\tgit-tab\n  \nNonTagLine";
        var tags = WingetParser.ParseTagsFromShowOutput(rawShow);
        Assert.Single(tags);
        Assert.Equal("git-tab", tags[0]);

        // 4. ParseDetailsList last item edge cases (empty ID, ARP ID)
        Assert.Empty(WingetParser.ParseDetailsList("(1) App []"));
        Assert.Empty(WingetParser.ParseDetailsList("(1) App [ARP\\Test]"));
    }

    [Fact]
    public async Task TriggerPackageAction_CancelInstallingPackage()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var service = App.Services.GetRequiredService<WingetService>();
            var pkg = new WingetPackage { Id = "Mock.CancelTest.App", Name = "Cancel Test", Status = PackageStatus.Installable };

            service.InstallPackage(pkg);
            Assert.True(pkg.IsInstalling);

            service.TriggerPackageAction(pkg);

            await TestHelper.WaitWhileAsync(() => pkg.IsInstalling, 2000);
            Assert.False(pkg.IsInstalling);
            Assert.Contains("Canceled", pkg.InstallStatusText);
        });
    }
}
