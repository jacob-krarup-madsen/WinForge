namespace WingetStore.Tests;

public class WingetPackageTests
{
    [Fact]
    public void CoreProperties_And_PropertyChanged()
    {
        var pkg = new WingetPackage();
        bool nameChanged = false;
        bool idChanged = false;
        pkg.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(pkg.Name)) nameChanged = true;
            if (e.PropertyName == nameof(pkg.Id)) idChanged = true;
        };

        pkg.Name = "Test Application";
        Assert.True(nameChanged);

        pkg.Id = "Test.Id";
        Assert.True(idChanged);
    }

    [Fact]
    public void IconUrlGetter_And_Caching()
    {
        var pkg = new WingetPackage { Id = "Test.App.Icon", Name = "Icon Test App" };
        var iconUrl = pkg.IconUrl;
        Assert.Equal("", iconUrl);
        var iconUrlCached = pkg.IconUrl;
        Assert.Equal(iconUrl, iconUrlCached);
    }

    [Fact]
    public void InitialParsing()
    {
        var pkg1 = new WingetPackage { Name = "Visual Studio Code" };
        var pkg2 = new WingetPackage { Name = "   git  " };
        var pkgNull = new WingetPackage { Name = null! };
        var pkgSpace = new WingetPackage { Name = "   " };

        Assert.Equal("V", pkg1.Initial);
        Assert.Equal("G", pkg2.Initial);
        Assert.Equal("?", pkgNull.Initial);
        Assert.Equal("?", pkgSpace.Initial);
    }

    [Fact]
    public void TagsInitialization()
    {
        var pkg = new WingetPackage();
        pkg.Tags.Add("developer");
        Assert.Single(pkg.Tags);
    }

    [Fact]
    public void ScreenshotsAndHasScreenshots()
    {
        var pkg = new WingetPackage { Id = "Mock.Screenshot.App", Name = "Screenshot App" };
        Assert.Equal(pkg.Screenshots.Count > 0, pkg.HasScreenshots);

        pkg.Screenshots = null!;
        var screenshots = pkg.Screenshots;
        Assert.Empty(screenshots);

        pkg.Screenshots = new List<string> { "url1" };
        Assert.Same(pkg.Screenshots, pkg.Screenshots);
    }

    [Fact]
    public void WingetPackage_PropertiesAndMethods_Comprehensive()
    {
        var pkg = new WingetPackage();

        // RecommendationReason coverage
        Assert.False(pkg.HasRecommendationReason);
        pkg.RecommendationReason = "Featured";
        Assert.Equal("Featured", pkg.RecommendationReason);
        Assert.True(pkg.HasRecommendationReason);

        // ActionButtonLabel coverage
        pkg.Status = PackageStatus.Installed;
        Assert.Equal("Uninstall", pkg.ActionButtonLabel);
        pkg.Status = PackageStatus.Upgradable;
        Assert.Equal("Update", pkg.ActionButtonLabel);
        pkg.Status = (PackageStatus)99;
        Assert.Equal("Install", pkg.ActionButtonLabel);
        pkg.Status = PackageStatus.Installable;
        Assert.Equal("Install", pkg.ActionButtonLabel);

        // RefreshIcon coverage
        pkg.IconUrl = "https://icon.com/logo.png";
        Assert.True(pkg.HasIcon);
        pkg.RefreshIcon();
        Assert.False(pkg.HasIcon);

        // Initial edge cases
        pkg.Name = "";
        Assert.Equal("?", pkg.Initial);
        pkg.Name = "   ";
        Assert.Equal("?", pkg.Initial);

        // MetadataItem coverage
        var metadata = new MetadataItem
        {
            Key = "Key",
            Value = "Value",
            IsUrl = true,
            SubItems = new List<MetadataItem>()
        };
        Assert.Equal("Key", metadata.Key);
        Assert.Equal("Value", metadata.Value);
        Assert.True(metadata.IsUrl);
        Assert.Empty(metadata.SubItems);
    }
}
