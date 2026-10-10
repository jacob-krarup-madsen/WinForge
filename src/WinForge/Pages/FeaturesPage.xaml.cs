using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ViVeToolApp.Models;
using WingetStore.ViewModels;

namespace WingetStore.Pages;

/// <summary>
/// WinUI 3 page for Windows 11 Feature Velocity (ViVeTool) management backed by <see cref="FeaturesViewModel"/>.
/// </summary>
public sealed partial class FeaturesPage : Page
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeaturesPage"/> class.
    /// </summary>
    public FeaturesPage()
    {
        ViewModel = App.Services.GetRequiredService<FeaturesViewModel>();
        InitializeComponent();
        Loaded += FeaturesPage_Loaded;
    }

    /// <summary>Gets the page view-model.</summary>
    public FeaturesViewModel ViewModel { get; }

    private void FeaturesPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadCatalog();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text ?? string.Empty;
    }

    private void FeaturesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = FeaturesListView.SelectedItems.Count > 0;
        EnableBtn.IsEnabled = hasSelection && ViewModel.IsIdle;
        DisableBtn.IsEnabled = hasSelection && ViewModel.IsIdle;
    }

    private async void EnableBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = FeaturesListView.SelectedItems.OfType<FeatureItem>().ToList();
        await ViewModel.ExecuteFeaturesAsync(enable: true, selected);
    }

    private async void DisableBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = FeaturesListView.SelectedItems.OfType<FeatureItem>().ToList();
        await ViewModel.ExecuteFeaturesAsync(enable: false, selected);
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCatalogAsync();
    }

    private async void DownloadViVeToolBtn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DownloadViVeToolAsync();
    }
}
