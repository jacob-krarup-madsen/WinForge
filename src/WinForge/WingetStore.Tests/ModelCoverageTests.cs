namespace WingetStore.Tests;

public class ModelCoverageTests
{
    [Fact]
    public void CategoryItem_Defaults()
    {
        var item = new CategoryItem();
        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(string.Empty, item.Tag);
        Assert.Equal("#1F0D4F", item.BackgroundColor);
        Assert.Equal("\uE943", item.IconGlyph);
    }

    [Fact]
    public void PackageStatusChangedMessage_Create()
    {
        var pkg = new WingetPackage { Id = "Test" };
        var msg = new PackageStatusChangedMessage(pkg);
        Assert.Same(pkg, msg.Value);
    }
}
