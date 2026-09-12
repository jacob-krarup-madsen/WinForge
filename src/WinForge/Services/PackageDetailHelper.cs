using WingetStore.Models;

namespace WingetStore.Services;

public static class PackageDetailHelper
{
    public static bool ShouldSkipMetadataItem(string key) => key switch { "Name" or "Version" or "Description" or "Release Notes" => true, _ => false };
    public static void PopulateMetadata(Microsoft.UI.Xaml.Controls.Panel container, IEnumerable<MetadataItem> details)
    {
        container.Children.Clear();
        foreach (var item in details)
        {
            var card = new Microsoft.UI.Xaml.Controls.Border { Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8), Padding = new Microsoft.UI.Xaml.Thickness(12), CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6), Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] };
            var stack = new Microsoft.UI.Xaml.Controls.StackPanel();
            if (!string.IsNullOrEmpty(item.Key)) stack.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = item.Key, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] });
            if (!string.IsNullOrEmpty(item.Value)) { if (item.IsUrl) stack.Children.Add(new Microsoft.UI.Xaml.Controls.HyperlinkButton { Content = item.Value, NavigateUri = new Uri(item.Value), Padding = new Microsoft.UI.Xaml.Thickness(0), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) }); else stack.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = item.Value, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) }); }
            foreach (var sub in item.SubItems)
            {
                var subStack = new Microsoft.UI.Xaml.Controls.StackPanel { Margin = new Microsoft.UI.Xaml.Thickness(12, 4, 0, 0) };
                if (!string.IsNullOrEmpty(sub.Key)) subStack.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = $"{sub.Key}:", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorTertiaryBrush"] });
                if (!string.IsNullOrEmpty(sub.Value)) { if (sub.IsUrl) subStack.Children.Add(new Microsoft.UI.Xaml.Controls.HyperlinkButton { Content = sub.Value, NavigateUri = new Uri(sub.Value), Padding = new Microsoft.UI.Xaml.Thickness(0) }); else subStack.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = sub.Value, FontSize = 12, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap }); }
                stack.Children.Add(subStack);
            }
            card.Child = stack; container.Children.Add(card);
        }
    }
}
