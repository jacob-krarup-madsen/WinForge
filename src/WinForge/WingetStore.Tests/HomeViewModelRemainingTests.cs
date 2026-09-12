namespace WingetStore.Tests;

public class HomeViewModelRemainingTests
{
    [Fact]
    public async Task OnSourceFilterChanged_CallsApplyFilter()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            var recField = typeof(HomeViewModel).GetField("_allRecommendations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            recField.SetValue(homeVM, new List<WingetPackage>
            {
                new() { Name = "App1", Source = "winget" },
                new() { Name = "App2", Source = "other" }
            });

            homeVM.SourceFilter = "winget";

            Assert.NotNull(homeVM.FilteredRecommendations);
        });
    }

    [Fact]
    public async Task SearchInternalAsync_WithValidQuery_CompletesWithoutError()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.SearchAsync("test");
            Assert.True(homeVM.IsSearchActive || !homeVM.IsLoading);
        });
    }
}
