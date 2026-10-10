using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using WingetStore.Pages;
using WingetStore.Services;
using WingetStore.ViewModels;
using Xunit;

namespace WingetStore.Tests;

[Trait("Category", "WinUIIntegration")]
public class WinUIPageCreationTests
{
    public WinUIPageCreationTests() { }

    [Fact]
    public void CanCreateSettingsPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(SettingsPage)) as SettingsPage;
        Assert.NotNull(page);
        Assert.IsType<SettingsPage>(page);
        Assert.NotNull(App.Services.GetService<ISettingsService>());
    }

    [Fact]
    public void CanCreateHomePage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(HomePage)) as HomePage;
        Assert.NotNull(page);
        Assert.IsType<HomePage>(page);
        var vm = App.Services.GetRequiredService<HomeViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CanCreateInstalledPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(InstalledPage)) as InstalledPage;
        Assert.NotNull(page);
        Assert.IsType<InstalledPage>(page);
        var vm = App.Services.GetRequiredService<InstalledViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CanCreateUpdatesPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(UpdatesPage)) as UpdatesPage;
        Assert.NotNull(page);
        Assert.IsType<UpdatesPage>(page);
        var vm = App.Services.GetRequiredService<UpdatesViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CanCreateDetailsPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(DetailsPage)) as DetailsPage;
        Assert.NotNull(page);
        Assert.IsType<DetailsPage>(page);
        Assert.NotNull(IconService.Instance);
    }

    [Fact]
    public void CanCreateAboutPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(AboutPage)) as AboutPage;
        Assert.NotNull(page);
        Assert.IsType<AboutPage>(page);
    }

    [Fact]
    public void CanCreateNoWingetPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(NoWingetPage)) as NoWingetPage;
        Assert.NotNull(page);
        Assert.IsType<NoWingetPage>(page);
    }

    [Fact]
    public void CanCreateOptimizerPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(OptimizerPage)) as OptimizerPage;
        Assert.NotNull(page);
        Assert.IsType<OptimizerPage>(page);
        var vm = App.Services.GetRequiredService<OptimizerViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CanCreateFeaturesPage()
    {
        var page = RuntimeHelpers.GetUninitializedObject(typeof(FeaturesPage)) as FeaturesPage;
        Assert.NotNull(page);
        Assert.IsType<FeaturesPage>(page);
        var vm = App.Services.GetRequiredService<FeaturesViewModel>();
        Assert.NotNull(vm);
    }
}
