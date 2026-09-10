using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class OfflineResourcePickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<OfflineResourceChoice> _native;
    private readonly IReadOnlyList<OfflineResourceChoice> _external;
    private string _searchText = string.Empty;
    private OfflineResourceChoice? _selectedChoice;

    public OfflineResourcePickerViewModel(
        IReadOnlyList<OfflineResourceChoice> native,
        IReadOnlyList<OfflineResourceChoice> external,
        string title)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _external = external ?? throw new ArgumentNullException(nameof(external));
        Title = string.IsNullOrWhiteSpace(title) ? "选择资源" : title.Trim();
        RefreshFilter();
    }

    public string Title { get; }
    public string Explanation => "选择当前项目中的资源，或选择已引用故事包中的资源。";
    public string NativeTabHeader => $"项目内引用 ({_native.Count})";
    public string ExternalTabHeader => $"项目外引用 ({_external.Count})";
    public ObservableCollection<OfflineResourceChoice> FilteredNative { get; } = [];
    public ObservableCollection<OfflineResourceChoice> FilteredExternal { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
        }
    }

    public OfflineResourceChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (!SetProperty(ref _selectedChoice, value)) return;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public bool CanConfirm => SelectedChoice is not null;
    public bool HasFilteredNative => FilteredNative.Count > 0;
    public bool HasFilteredExternal => FilteredExternal.Count > 0;

    public string NativeEmptyText => _native.Count == 0 ? "当前项目没有可引用的原生资源。" : "没有匹配的项目内资源。";
    public string ExternalEmptyText => _external.Count == 0 ? "references/ 中没有可引用的外部资源。" : "没有匹配的项目外资源。";

    public void Select(OfflineResourceChoice? choice) => SelectedChoice = choice;

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        Replace(FilteredNative, _native.Where(choice => Matches(choice, query)));
        Replace(FilteredExternal, _external.Where(choice => Matches(choice, query)));

        if (SelectedChoice is not null &&
            !FilteredNative.Contains(SelectedChoice) &&
            !FilteredExternal.Contains(SelectedChoice))
        {
            SelectedChoice = null;
        }

        OnPropertyChanged(nameof(HasFilteredNative));
        OnPropertyChanged(nameof(HasFilteredExternal));
    }

    private static bool Matches(OfflineResourceChoice choice, string query)
    {
        if (query.Length == 0) return true;
        return Contains(choice.DisplayName, query) ||
               Contains(choice.Id, query) ||
               Contains(choice.Kind, query) ||
               Contains(choice.Provider, query);
    }

    private static bool Contains(string value, string query) =>
        value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;

    private static void Replace(
        ObservableCollection<OfflineResourceChoice> target,
        IEnumerable<OfflineResourceChoice> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}
