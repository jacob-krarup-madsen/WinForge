using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Controls;

public enum ResponsiveBand { Narrow, Medium, Wide }

public sealed class ResponsivePageContainer : ContentControl
{
    private ResponsiveBand? _currentBand;

    public ResponsivePageContainer()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        UpdatePadding(ActualWidth);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        _currentBand = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePadding(e.NewSize.Width);

    public static ResponsiveBand GetBand(double width) => width switch
    {
        < 700 => ResponsiveBand.Narrow,
        < 1200 => ResponsiveBand.Medium,
        _ => ResponsiveBand.Wide
    };

    public static Thickness GetPadding(ResponsiveBand band) => band switch
    {
        ResponsiveBand.Narrow => new Thickness(16, 16, 16, 24),
        ResponsiveBand.Medium => new Thickness(24, 20, 24, 28),
        _ => new Thickness(32, 24, 32, 32)
    };

    private void UpdatePadding(double width)
    {
        ResponsiveBand band = GetBand(width);
        if (_currentBand == band) return;
        _currentBand = band;
        Padding = GetPadding(band);
    }
}
