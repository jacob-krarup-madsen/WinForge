using WingetStore.Models;

namespace WingetStore.Services;

public class BulkSelectionHelper(Action onSelectionChanged)
{
    private readonly Action _onSelectionChanged = onSelectionChanged;
    public bool IsActive { get; set; }
    public List<WingetPackage> SelectedPackages { get; set; } = [];
    public void Toggle() { IsActive = !IsActive; if (!IsActive) SelectedPackages.Clear(); _onSelectionChanged(); }
    public void SelectAll(IEnumerable<WingetPackage> packages) { SelectedPackages = [.. packages]; _onSelectionChanged(); }
    public void DeselectAll() { SelectedPackages.Clear(); _onSelectionChanged(); }
    public static bool? ComputeSelectAllState(int totalCount, int selectedCount) { if (totalCount == 0 || selectedCount == 0) return false; if (selectedCount == totalCount) return true; return null; }
}
