namespace WingetStore.Tests;

public class CachingWingetServiceStaticTests
{
    [Fact]
    public void MergePackageProperties_NullArguments_ThrowsArgumentNullException()
    {
        var pkg = new WingetPackage { Id = "P1" };
        Assert.Throws<ArgumentNullException>(() => CachingWingetService.MergePackageProperties(null!, pkg));
        Assert.Throws<ArgumentNullException>(() => CachingWingetService.MergePackageProperties(pkg, null!));
    }

    [Fact]
    public void MergePackageProperties_OverwritesNonNullProperties()
    {
        var existing = new WingetPackage { Id = "P1", Name = "OldName", Version = "1.0" };
        var incoming = new WingetPackage { Id = "P1", Name = "NewName", Version = "2.0", Publisher = "NewPub", Source = "winget" };

        CachingWingetService.MergePackageProperties(existing, incoming);

        Assert.Equal("NewName", existing.Name);
        Assert.Equal("2.0", existing.Version);
        Assert.Equal("NewPub", existing.Publisher);
        Assert.Equal("winget", existing.Source);
    }

    [Fact]
    public void MergePackageProperties_PreservesExistingWhenIncomingEmpty()
    {
        var existing = new WingetPackage { Id = "P1.App", Name = "OldName", Version = "1.0", Description = "ExistingDesc" };
        var incoming = new WingetPackage { Id = "P1.App", Name = "NewName", Version = "", Description = "" };

        CachingWingetService.MergePackageProperties(existing, incoming);

        Assert.Equal("NewName", existing.Name);
        Assert.Equal("1.0", existing.Version);
        Assert.Equal("ExistingDesc", existing.Description);
    }

    [Fact]
    public void MergePackageProperties_StatusTransitions_UpdatesNonInstallable()
    {
        var existing = new WingetPackage { Id = "P1", Status = PackageStatus.Installable };
        var incoming = new WingetPackage { Id = "P1", Status = PackageStatus.Installed };

        CachingWingetService.MergePackageProperties(existing, incoming);
        Assert.Equal(PackageStatus.Installed, existing.Status);

        var incomingInstallable = new WingetPackage { Id = "P1", Status = PackageStatus.Installable };
        CachingWingetService.MergePackageProperties(existing, incomingInstallable);
        Assert.Equal(PackageStatus.Installed, existing.Status);
    }

    [Fact]
    public void MergePackageProperties_ListCollections_CopiesNonEmptyLists()
    {
        var existing = new WingetPackage { Id = "P1" };
        var incoming = new WingetPackage
        {
            Id = "P1",
            Tags = ["tag1", "tag2"],
            Screenshots = ["shot1.png"]
        };

        CachingWingetService.MergePackageProperties(existing, incoming);

        Assert.Equal(2, existing.Tags.Count);
        Assert.Single(existing.Screenshots);
    }
}
