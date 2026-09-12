using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using WingetStore.Models;

namespace WingetStore.ViewModels;

public sealed class RecommendationLayoutState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private Thickness _cardMargin = new Thickness(0, 0, 16, 16);
    public Thickness CardMargin
    {
        get => _cardMargin;
        set
        {
            if (_cardMargin != value)
            {
                _cardMargin = value;
                OnPropertyChanged();
            }
        }
    }

    private double _cardHeight = 146.0;
    public double CardHeight
    {
        get => _cardHeight;
        set
        {
            if (_cardHeight != value)
            {
                _cardHeight = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RecommendationCardViewModel
{
    public WingetPackage Package { get; }
    public RecommendationLayoutState LayoutState { get; }

    public RecommendationCardViewModel(WingetPackage package, RecommendationLayoutState layoutState)
    {
        Package = package;
        LayoutState = layoutState;
    }
}
