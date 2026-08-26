using System.Collections.ObjectModel;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record NavigationItem(
    string Page,
    string Title,
    Geometry IconGeometry,
    bool IsEnabled = true)
{
    public Geometry Icon => IconGeometry;
}

public sealed class NavigationViewModel : ObservableObject
{
    private readonly ReadOnlyCollection<NavigationItem> _items;
    private NavigationItem _selectedItem;

    public NavigationViewModel()
    {
        _items = new ReadOnlyCollection<NavigationItem>(
        [
            new(
                "Story",
                "剧情",
                Geometry.Parse("M3,4 C6,3 9,4 11,6 V20 C8,18 6,18 3,19 Z M13,6 C15,4 18,3 21,4 V19 C18,18 16,18 13,20 Z")),
            new(
                "Settings",
                "设置",
                Geometry.Parse("M10.5,3 H13.5 L14.2,5.2 A7,7 0 0 1 16.1,6.3 L18.4,5.5 L20.5,9.1 L18.6,10.6 A7,7 0 0 1 18.6,13.4 L20.5,14.9 L18.4,18.5 L16.1,17.7 A7,7 0 0 1 14.2,18.8 L13.5,21 H10.5 L9.8,18.8 A7,7 0 0 1 7.9,17.7 L5.6,18.5 L3.5,14.9 L5.4,13.4 A7,7 0 0 1 5.4,10.6 L3.5,9.1 L5.6,5.5 L7.9,6.3 A7,7 0 0 1 9.8,5.2 Z M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9"))
        ]);
        _selectedItem = _items[0];
    }

    public IReadOnlyList<NavigationItem> Items => _items;

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetProperty(ref _selectedItem, value);
        }
    }
}
