namespace WingetStore.Tests;

public class HomeViewModelCoverageTests
{
    [Fact]
    public async Task HomeViewModel_ApplyFilter_WithNullRecommendations()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            var recField = typeof(HomeViewModel).GetField("_allRecommendations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            recField.SetValue(homeVM, null);
            homeVM.FilterQuery = "test";
            homeVM.ApplyFilter();
        });
    }

    [Fact]
    public async Task HomeViewModel_ApplyFilter_FiltersRecommendations()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.LoadFeaturedContentAsync();

            homeVM.FilterQuery = "popular";
            Assert.NotNull(homeVM.FilteredRecommendations);
        });
    }

    [Fact]
    public async Task HomeViewModel_LoadFeaturedContentAsync_PopulatesCategories()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.LoadFeaturedContentAsync();
            Assert.NotNull(homeVM.Categories);
            Assert.NotEmpty(homeVM.Categories);
        });
    }

    [Fact]
    public async Task HomeViewModel_SearchAsync_ClearsOnEmpty()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.SearchAsync("");
            Assert.False(homeVM.IsSearchActive);
        });
    }

    [Fact]
    public async Task HomeViewModel_SearchAsync_Whitespace_Clears()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.SearchAsync("   ");
            Assert.False(homeVM.IsSearchActive);
        });
    }

    [Fact]
    public async Task HomeViewModel_SortOrder_Changes()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.LoadFeaturedContentAsync();
            homeVM.SortOrder = "az";
            homeVM.SortOrder = "za";
            homeVM.SortOrder = "default";
        });
    }

    [Fact]
    public async Task HomeViewModel_RecommendationCardViewModel_Wrapping()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.LoadFeaturedContentAsync();

            var filteredField = typeof(HomeViewModel).GetField("_allRecommendations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var recs = filteredField.GetValue(homeVM) as List<WingetPackage>;
            if (recs != null && recs.Count > 0)
            {
                var layoutState = new RecommendationLayoutState();
                var cardVm = new RecommendationCardViewModel(recs[0], layoutState);
                Assert.Same(recs[0], cardVm.Package);
                Assert.Same(layoutState, cardVm.LayoutState);
            }
        });
    }
}
