namespace WingetStore.Tests;

public class RecommendationLayoutTests
{
    [Fact]
    public void RecommendationLayoutState_DefaultValues()
    {
        var state = new RecommendationLayoutState();
        Assert.Equal(146.0, state.CardHeight);
        Assert.Equal(new Microsoft.UI.Xaml.Thickness(0, 0, 16, 16), state.CardMargin);
    }

    [Fact]
    public void RecommendationLayoutState_CardHeight_RaisesPropertyChanged()
    {
        var state = new RecommendationLayoutState();
        string? changedProp = null;
        state.PropertyChanged += (s, e) => changedProp = e.PropertyName;
        state.CardHeight = 200;
        Assert.Equal(200, state.CardHeight);
        Assert.Equal(nameof(RecommendationLayoutState.CardHeight), changedProp);
    }

    [Fact]
    public void RecommendationLayoutState_CardMargin_RaisesPropertyChanged()
    {
        var state = new RecommendationLayoutState();
        string? changedProp = null;
        state.PropertyChanged += (s, e) => changedProp = e.PropertyName;
        state.CardMargin = new Microsoft.UI.Xaml.Thickness(8);
        Assert.Equal(new Microsoft.UI.Xaml.Thickness(8), state.CardMargin);
        Assert.Equal(nameof(RecommendationLayoutState.CardMargin), changedProp);
    }

    [Fact]
    public void RecommendationLayoutState_SameValue_DoesNotRaisePropertyChanged()
    {
        var state = new RecommendationLayoutState();
        int changeCount = 0;
        state.PropertyChanged += (s, e) => changeCount++;
        state.CardHeight = 146.0;
        state.CardMargin = new Microsoft.UI.Xaml.Thickness(0, 0, 16, 16);
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void RecommendationCardViewModel_StoresPackageAndLayout()
    {
        var pkg = new WingetPackage { Id = "Test.App", Name = "Test App" };
        var layout = new RecommendationLayoutState();
        var vm = new RecommendationCardViewModel(pkg, layout);
        Assert.Same(pkg, vm.Package);
        Assert.Same(layout, vm.LayoutState);
    }
}
