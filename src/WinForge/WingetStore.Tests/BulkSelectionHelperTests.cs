namespace WingetStore.Tests;

public class BulkSelectionHelperTests
{
    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(0, 5, false)]
    [InlineData(3, 5, null)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 5, null)]
    public void ComputeSelectAllState_ReturnsExpected(int selected, int total, bool? expected)
    {
        Assert.Equal(expected, BulkSelectionHelper.ComputeSelectAllState(selected, total));
    }
}
