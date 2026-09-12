namespace WingetStore.Tests;

public class AppHelperTests
{
    [Theory]
    [InlineData(true, Microsoft.UI.Xaml.Visibility.Visible)]
    [InlineData(false, Microsoft.UI.Xaml.Visibility.Collapsed)]
    public void VisibleIf_ReturnsCorrectVisibility(bool value, Microsoft.UI.Xaml.Visibility expected)
    {
        Assert.Equal(expected, App.VisibleIf(value));
    }

    [Theory]
    [InlineData(true, Microsoft.UI.Xaml.Visibility.Collapsed)]
    [InlineData(false, Microsoft.UI.Xaml.Visibility.Visible)]
    public void CollapsedIf_ReturnsCorrectVisibility(bool value, Microsoft.UI.Xaml.Visibility expected)
    {
        Assert.Equal(expected, App.CollapsedIf(value));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_ReturnsInverse(bool value, bool expected)
    {
        Assert.Equal(expected, App.Not(value));
    }

    [Fact]
    public void Dispatch_UsesDispatcherOverride()
    {
        bool invoked = false;
        var original = App.DispatcherOverride;
        try
        {
            App.DispatcherOverride = action => invoked = true;
            App.Dispatch(() => { });
            Assert.True(invoked);
        }
        finally
        {
            App.DispatcherOverride = original;
        }
    }

    [Fact]
    public void Dispatch_NoOverride_FallsThrough()
    {
        var original = App.DispatcherOverride;
        try
        {
            App.DispatcherOverride = null;
            bool invoked = false;
            App.Dispatch(() => invoked = true);
            Assert.True(invoked);
        }
        finally
        {
            App.DispatcherOverride = original;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToImageSource_NullOrEmpty_ReturnsNull(string? path)
    {
        Assert.Null(App.ToImageSource(path));
    }

    [Fact]
    public void ToImageSource_ValidUri_CatchBlockReturnsNull()
    {
        Assert.Null(App.ToImageSource("http://example.com/icon.png"));
    }

    [Fact]
    public void IsUITestMode_ReturnsFalseDuringNormalTests()
    {
        Assert.False(App.IsUITestMode());
    }
}
