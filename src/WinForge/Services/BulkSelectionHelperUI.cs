namespace WingetStore.Services;

public class BulkSelectionHelperUI(Microsoft.UI.Xaml.Controls.ListView listView, Microsoft.UI.Xaml.Controls.Button actionButton, Microsoft.UI.Xaml.Controls.TextBlock countText, Microsoft.UI.Xaml.Controls.CheckBox selectAllCheckBox, Microsoft.UI.Xaml.UIElement actionBar, Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggleButton)
{
    private readonly Microsoft.UI.Xaml.Controls.ListView _listView = listView; private readonly Microsoft.UI.Xaml.Controls.Button _actionButton = actionButton; private readonly Microsoft.UI.Xaml.Controls.TextBlock _countText = countText; private readonly Microsoft.UI.Xaml.Controls.CheckBox _selectAllCheckBox = selectAllCheckBox; private readonly Microsoft.UI.Xaml.UIElement _actionBar = actionBar; private readonly Microsoft.UI.Xaml.Controls.Primitives.ToggleButton _toggleButton = toggleButton; private bool _isUpdatingSelection;
    public void Toggle() { if (_toggleButton.IsChecked == true) Activate(); else Deactivate(); }
    public void Activate() { _toggleButton.IsChecked = true; _listView.SelectionMode = Microsoft.UI.Xaml.Controls.ListViewSelectionMode.Multiple; _listView.IsItemClickEnabled = false; _actionBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible; UpdateSelectionUI(); }
    public void Deactivate() { _toggleButton.IsChecked = false; try { _listView.SelectedItems.Clear(); } catch { } _listView.SelectionMode = Microsoft.UI.Xaml.Controls.ListViewSelectionMode.None; _listView.IsItemClickEnabled = true; _actionBar.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed; UpdateSelectionUI(); }
    public void Cancel() => Deactivate();
    public void SelectAll() { _isUpdatingSelection = true; _listView.SelectAll(); _isUpdatingSelection = false; UpdateSelectionUI(); }
    public void DeselectAll() { _isUpdatingSelection = true; _listView.SelectedItems.Clear(); _isUpdatingSelection = false; UpdateSelectionUI(); }
    public void OnSelectionChanged() { if (!_isUpdatingSelection) UpdateSelectionUI(); }
    private void UpdateSelectionUI() { int count = _listView.SelectedItems.Count; int total = _listView.Items.Count; _actionButton.IsEnabled = count > 0; _countText.Text = $"{count} app{(count == 1 ? "" : "s")} selected"; _isUpdatingSelection = true; _selectAllCheckBox.IsChecked = total > 0 && count == total ? true : count > 0 ? null : false; _isUpdatingSelection = false; }
}
