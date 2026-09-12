using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WingetStore.Models;

public partial class InstallTask : INotifyPropertyChanged
{
    private InstallTaskStatus _status = InstallTaskStatus.Queued;
    private double _progress;
    private string _statusText = "Queued";
    private string _logOutput = "";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PackageId { get; set; } = "";
    public string PackageName { get; set; } = "";
    public TaskOperation Operation { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public bool CanCancel => Status == InstallTaskStatus.Running || Status == InstallTaskStatus.Queued;
    public InstallTaskStatus Status { get => _status; set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCancel)); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public string LogOutput { get => _logOutput; set { _logOutput = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

