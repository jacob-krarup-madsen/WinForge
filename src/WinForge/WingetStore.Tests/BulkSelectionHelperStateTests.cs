namespace WingetStore.Tests;

public class BulkSelectionHelperStateTests
{
    [Fact]
    public void BulkSelectionHelper_Toggle_ActivatesAndDeactivates()
    {
        int callbackCount = 0;
        var helper = new BulkSelectionHelper(() => callbackCount++);
        Assert.False(helper.IsActive);
        Assert.Empty(helper.SelectedPackages);

        helper.Toggle();
        Assert.True(helper.IsActive);
        Assert.Empty(helper.SelectedPackages);
        Assert.Equal(1, callbackCount);

        helper.Toggle();
        Assert.False(helper.IsActive);
        Assert.Empty(helper.SelectedPackages);
        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public void BulkSelectionHelper_SelectAll_AddsPackages()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "A" }, new() { Id = "B" }
        };
        var helper = new BulkSelectionHelper(() => { });
        helper.SelectAll(packages);
        Assert.Equal(2, helper.SelectedPackages.Count);
    }

    [Fact]
    public void BulkSelectionHelper_DeselectAll_ClearsPackages()
    {
        var helper = new BulkSelectionHelper(() => { });
        helper.SelectAll(new List<WingetPackage> { new() { Id = "A" } });
        Assert.Single(helper.SelectedPackages);
        helper.DeselectAll();
        Assert.Empty(helper.SelectedPackages);
    }
}
