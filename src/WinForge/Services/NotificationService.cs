namespace WingetStore.Services;

public class NotificationService : INotificationService
{
    private static void ShowDialog(string title, string message, string logPrefix) => App.Dispatch(async () => { try { if (!SettingsService.EnableNotifications) return; if (App.MainWindow?.Content?.XamlRoot is Microsoft.UI.Xaml.XamlRoot xamlRoot) { var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = xamlRoot }; await dialog.ShowAsync(); } else LogService.LogInfo($"Could not display ContentDialog '{title}': XamlRoot is null."); } catch (Exception ex) { LogService.LogError($"{logPrefix} ContentDialog.ShowAsync failed", ex); } });
    public void ShowError(string title, string message) => ShowDialog(title, message, "ShowError");
    public void ShowInfo(string title, string message) => ShowDialog(title, message, "ShowInfo");
}
