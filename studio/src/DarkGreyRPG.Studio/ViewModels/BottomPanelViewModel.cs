using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record BottomPanelTab(
    string Page,
    string Title,
    string Glyph,
    bool IsEnabled = true);

public sealed class BottomPanelViewModel : ObservableObject
{
    public const double HeaderHeight = 42;
    public const double DefaultExpandedHeight = 220;
    public const double MinimumExpandedHeight = 120;
    public const double MaximumExpandedHeight = 520;

    private readonly ReadOnlyCollection<BottomPanelTab> _tabs;
    private BottomPanelTab _selectedTab;
    private bool _isExpanded;
    private double _expandedHeight = DefaultExpandedHeight;

    public BottomPanelViewModel()
    {
        _tabs = new ReadOnlyCollection<BottomPanelTab>(
        [
            new("Output", "输出", "Output"),
            new("Problems", "问题", "Problems"),
            new("Debugger", "调试器", "Debugger"),
            new("Minecraft", "Minecraft", "Minecraft")
        ]);
        _selectedTab = _tabs[0];
    }

    public IReadOnlyList<BottomPanelTab> Tabs => _tabs;

    public BottomPanelTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetProperty(ref _selectedTab, value);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(DockHeight));
            }
        }
    }

    public double ExpandedHeight
    {
        get => _expandedHeight;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < MinimumExpandedHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Expanded height must be at least {MinimumExpandedHeight}.");
            }

            if (SetProperty(ref _expandedHeight, Math.Min(value, MaximumExpandedHeight)))
            {
                OnPropertyChanged(nameof(DockHeight));
            }
        }
    }

    public double DockHeight => IsExpanded ? ExpandedHeight : HeaderHeight;

    public void Toggle() => IsExpanded = !IsExpanded;

    public void SelectTab(BottomPanelTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (ReferenceEquals(tab, SelectedTab) || tab == SelectedTab)
        {
            Toggle();
            return;
        }

        SelectedTab = tab;
        IsExpanded = true;
    }
}
