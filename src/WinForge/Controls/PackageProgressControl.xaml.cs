using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Controls;

public sealed partial class PackageProgressControl : UserControl
{
    public PackageProgressControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PackageProgressControl), new PropertyMetadata(""));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(PackageProgressControl), new PropertyMetadata(0d));

    public static readonly DependencyProperty IsInstallingProperty =
        DependencyProperty.Register(nameof(IsInstalling), typeof(bool), typeof(PackageProgressControl), new PropertyMetadata(false, OnIsInstallingChanged));

    public static readonly DependencyProperty StatusPanelMinWidthProperty =
        DependencyProperty.Register(nameof(StatusPanelMinWidth), typeof(double), typeof(PackageProgressControl), new PropertyMetadata(0d));

    public static readonly DependencyProperty LogButtonMarginProperty =
        DependencyProperty.Register(nameof(LogButtonMargin), typeof(Thickness), typeof(PackageProgressControl), new PropertyMetadata(new Thickness(8, 0, 0, 0)));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsInstalling
    {
        get => (bool)GetValue(IsInstallingProperty);
        set => SetValue(IsInstallingProperty, value);
    }

    public double StatusPanelMinWidth
    {
        get => (double)GetValue(StatusPanelMinWidthProperty);
        set => SetValue(StatusPanelMinWidthProperty, value);
    }

    public Thickness LogButtonMargin
    {
        get => (Thickness)GetValue(LogButtonMarginProperty);
        set => SetValue(LogButtonMarginProperty, value);
    }

    public event RoutedEventHandler? LogRequested;

    private void LogButton_Click(object sender, RoutedEventArgs e) => LogRequested?.Invoke(sender, e);

    private static void OnIsInstallingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PackageProgressControl control)
            control.RootGrid.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }
}
