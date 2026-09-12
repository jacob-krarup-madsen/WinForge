namespace WingetStore.Tests;

public class PackageFilteringHelperTests
{
    [Fact]
    public void MatchesQuery_NullPackage_ReturnsFalse()
    {
        WingetPackage? pkg = null;
        Assert.False(pkg!.MatchesQuery("test"));
    }


    [Fact]
    public void MatchesQuery_EmptyQuery_ReturnsTrue()
    {
        var pkg = new WingetPackage { Id = "App", Name = "App Name" };
        Assert.True(pkg.MatchesQuery(null!));
        Assert.True(pkg.MatchesQuery(""));
        Assert.True(pkg.MatchesQuery("   "));
    }

    [Fact]
    public void MatchesQuery_ValidMatches_ReturnsTrue()
    {
        var pkg = new WingetPackage { Id = "Git.Git", Name = "Git Installer", Publisher = "Software Corp" };

        // ID Match
        Assert.True(pkg.MatchesQuery("git"));
        // Name Match
        Assert.True(pkg.MatchesQuery("installer"));
        // Publisher Match
        Assert.True(pkg.MatchesQuery("corp"));
        // Case insensitive Match
        Assert.True(pkg.MatchesQuery("SOFTWARE"));
    }

    [Fact]
    public void MatchesQuery_Mismatches_ReturnsFalse()
    {
        var pkg = new WingetPackage { Id = "Git.Git", Name = "Git Installer", Publisher = "Software Corp" };
        Assert.False(pkg.MatchesQuery("vscode"));
    }

    [Fact]
    public void MatchesQuery_NullProperties_ReturnsFalse()
    {
        var pkg = new WingetPackage { Id = null!, Name = null!, Publisher = null! };
        Assert.False(pkg.MatchesQuery("test"));
    }
}
