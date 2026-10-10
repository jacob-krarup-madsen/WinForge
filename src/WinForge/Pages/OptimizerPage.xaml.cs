using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetStore.Services;
using WingetStore.ViewModels;

namespace WingetStore.Pages;

/// <summary>
/// WinUI 3 page for native Windows 11 system optimization, disk cleanup, memory flushing, auditing, and benchmarking.
/// </summary>
public sealed partial class OptimizerPage : Page
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizerPage"/> class.
    /// </summary>
    public OptimizerPage()
    {
        ViewModel = App.Services.GetRequiredService<OptimizerViewModel>();
        InitializeComponent();
        Loaded += OptimizerPage_Loaded;
    }

    /// <summary>Gets the page view-model.</summary>
    public OptimizerViewModel ViewModel { get; }

    private void OptimizerPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.CheckElevation();
    }

    private async void OptimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunOptimizeAsync();
    }

    private async void CleanDiskBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CleanDiskAsync();
    }

    private async void FlushMemoryBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.FlushMemoryAsync();
    }

    private async void AuditBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunAuditAsync();
    }

    private async void BenchmarkBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunBenchmarkAsync();
    }

    private async void UndoBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.UndoOptimizationsAsync();
    }

    private async void ExportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExportConfigAsync(AppPaths.OptimizerConfigFile);
    }

    private async void ImportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ImportConfigAsync(AppPaths.OptimizerConfigFile);
    }
}
