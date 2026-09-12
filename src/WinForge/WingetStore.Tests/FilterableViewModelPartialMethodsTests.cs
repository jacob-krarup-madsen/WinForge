namespace WingetStore.Tests;

public class FilterableViewModelPartialMethodsTests
{
    private class TestFilterableViewModel : FilterableViewModel
    {
        public int ApplyFilterCallCount { get; set; }
        public override void ApplyFilter() => ApplyFilterCallCount++;
    }

    [Fact]
    public void OnCategoryFilterChanged_RaisesPropertyChangedForIsCategoryProperties()
    {
        var vm = new TestFilterableViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CategoryFilter = "All";

        Assert.Contains("IsCategoryApps", changed);
        Assert.Contains("IsCategoryRedist", changed);
        Assert.Contains("IsCategoryAll", changed);
        Assert.Equal(1, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void OnCategoryFilterChanged_MultipleChanges()
    {
        var vm = new TestFilterableViewModel();

        vm.CategoryFilter = "Redist";
        Assert.Equal(1, vm.ApplyFilterCallCount);

        vm.CategoryFilter = "All";
        Assert.Equal(2, vm.ApplyFilterCallCount);

        vm.CategoryFilter = "Apps";
        Assert.Equal(3, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void OnAppsCountChanged_RaisesPropertyChanged()
    {
        var vm = new TestFilterableViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.AppsCount = 42;

        Assert.Contains("AppsCountText", changed);
    }

    [Fact]
    public void OnRedistCountChanged_RaisesPropertyChanged()
    {
        var vm = new TestFilterableViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.RedistCount = 10;

        Assert.Contains("RedistCountText", changed);
    }

    [Fact]
    public void OnTotalCountChanged_RaisesPropertyChanged()
    {
        var vm = new TestFilterableViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.TotalCount = 100;

        Assert.Contains("AllCountText", changed);
    }

    [Fact]
    public void OnFilterQueryChanged_CallsApplyFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.FilterQuery = "test";
        Assert.Equal(1, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void OnSortOrderChanged_CallsApplyFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.SortOrder = "az";
        Assert.Equal(1, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void OnSortByChanged_CallsApplyFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.SortBy = "Version";
        Assert.Equal(1, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void OnSortDirectionChanged_CallsApplyFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.SortDirection = "Descending";
        Assert.Equal(1, vm.ApplyFilterCallCount);
    }

    [Fact]
    public void ComputedPropertyGetters_ReturnFormattedValues()
    {
        var vm = new TestFilterableViewModel();

        vm.AppsCount = 5;
        Assert.Equal("Applications (5)", vm.AppsCountText);

        vm.RedistCount = 3;
        Assert.Equal("Redistributables (3)", vm.RedistCountText);

        vm.TotalCount = 8;
        Assert.Equal("All (8)", vm.AllCountText);
    }

    [Fact]
    public void IsCategoryGetters_MatchCurrentCategoryFilter()
    {
        var vm = new TestFilterableViewModel();

        vm.CategoryFilter = "Apps";
        Assert.True(vm.IsCategoryApps);
        Assert.False(vm.IsCategoryRedist);
        Assert.False(vm.IsCategoryAll);

        vm.CategoryFilter = "Redist";
        Assert.False(vm.IsCategoryApps);
        Assert.True(vm.IsCategoryRedist);
        Assert.False(vm.IsCategoryAll);

        vm.CategoryFilter = "All";
        Assert.False(vm.IsCategoryApps);
        Assert.False(vm.IsCategoryRedist);
        Assert.True(vm.IsCategoryAll);
    }

    [Fact]
    public void IsCategoryApp_Setter_ChangesCategoryFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.CategoryFilter = "All";
        vm.IsCategoryApps = true;
        Assert.Equal("Apps", vm.CategoryFilter);
    }

    [Fact]
    public void IsCategoryRedist_Setter_ChangesCategoryFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.CategoryFilter = "All";
        vm.IsCategoryRedist = true;
        Assert.Equal("Redist", vm.CategoryFilter);
    }

    [Fact]
    public void IsCategoryAll_Setter_ChangesCategoryFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.CategoryFilter = "Apps";
        vm.IsCategoryAll = true;
        Assert.Equal("All", vm.CategoryFilter);
    }

    [Fact]
    public void IsCategorySetter_WhenFalse_DoesNotChangeCategoryFilter()
    {
        var vm = new TestFilterableViewModel();
        vm.CategoryFilter = "All";

        vm.IsCategoryApps = false;
        Assert.Equal("All", vm.CategoryFilter);

        vm.IsCategoryRedist = false;
        Assert.Equal("All", vm.CategoryFilter);

        vm.IsCategoryAll = false;
        Assert.Equal("All", vm.CategoryFilter);
    }

    [Fact]
    public void IsCategorySetter_WhenAlreadyMatch_DoesNotChange()
    {
        var vm = new TestFilterableViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("Apps", vm.CategoryFilter);
        vm.IsCategoryApps = true;

        Assert.DoesNotContain("CategoryFilter", changed);
        Assert.Equal(0, vm.ApplyFilterCallCount);
    }
}
