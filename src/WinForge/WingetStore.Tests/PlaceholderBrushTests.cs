namespace WingetStore.Tests;

public class PlaceholderBrushTests
{
    [Fact]
    public void GetPlaceholderColorForName_EmptyName_ReturnsGray()
    {
        var color = WingetPackage.GetPlaceholderColorForName("");
        Assert.Equal(Microsoft.UI.Colors.Gray, color);
    }

    [Fact]
    public void GetPlaceholderColorForName_NullName_ReturnsGray()
    {
        var color = WingetPackage.GetPlaceholderColorForName(null!);
        Assert.Equal(Microsoft.UI.Colors.Gray, color);
    }

    [Fact]
    public void GetPlaceholderColorForName_WhitespaceName_ReturnsGray()
    {
        var color = WingetPackage.GetPlaceholderColorForName("   ");
        Assert.Equal(Microsoft.UI.Colors.Gray, color);
    }

    [Fact]
    public void GetPlaceholderColorForName_ValidName_ReturnsNonTransparentColor()
    {
        var color = WingetPackage.GetPlaceholderColorForName("Git");
        Assert.NotEqual(Microsoft.UI.Colors.Transparent, color);
        Assert.NotEqual(Microsoft.UI.Colors.Gray, color);
    }

    [Fact]
    public void GetPlaceholderColorForName_DifferentNames_DifferentColors()
    {
        var color1 = WingetPackage.GetPlaceholderColorForName("A");
        var color2 = WingetPackage.GetPlaceholderColorForName("B");
        Assert.NotEqual(color1, color2);
    }

    [Fact]
    public void GetPlaceholderColorForName_SameName_ConsistentColor()
    {
        var color1 = WingetPackage.GetPlaceholderColorForName("Node.js");
        var color2 = WingetPackage.GetPlaceholderColorForName("Node.js");
        Assert.Equal(color1, color2);
    }

    [Fact]
    public void GetPlaceholderColorForName_CommonPackageNames_ReturnsOneOfTenColors()
    {
        var knownColors = new Windows.UI.Color[]
        {
            Windows.UI.Color.FromArgb(255, 30, 144, 255),
            Windows.UI.Color.FromArgb(255, 46, 139, 87),
            Windows.UI.Color.FromArgb(255, 138, 43, 226),
            Windows.UI.Color.FromArgb(255, 210, 105, 30),
            Windows.UI.Color.FromArgb(255, 220, 20, 60),
            Windows.UI.Color.FromArgb(255, 0, 128, 128),
            Windows.UI.Color.FromArgb(255, 218, 112, 214),
            Windows.UI.Color.FromArgb(255, 255, 99, 71),
            Windows.UI.Color.FromArgb(255, 70, 130, 180),
            Windows.UI.Color.FromArgb(255, 186, 85, 211)
        };

        foreach (var name in new[] { "Git", "Python", "Node.js", "Firefox", "VS Code" })
        {
            var color = WingetPackage.GetPlaceholderColorForName(name);
            Assert.Contains(color, knownColors);
        }
    }
}
