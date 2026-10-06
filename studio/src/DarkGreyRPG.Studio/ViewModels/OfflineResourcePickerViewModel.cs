using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class OfflineResourceFolder : ObservableObject
{
    private bool _expanded;
    internal OfflineResourceFolder(string name, IReadOnlyList<OfflineResourceChoice> resources)
    { DisplayName = name; Resources = resources; }
    public string DisplayName { get; }
    internal IReadOnlyList<OfflineResourceChoice> Resources { get; }
    public ObservableCollection<OfflineResourceChoice> Matches { get; } = [];
    public string Header => $"{DisplayName} ({Matches.Count})";
    public bool IsExpanded { get => _expanded; set => SetProperty(ref _expanded, value); }
    internal void Filter(string query)
    {
        var matches = Resources.Where(choice => query.Length == 0 ||
            choice.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            choice.TypeLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        Matches.Clear(); foreach (var choice in matches) Matches.Add(choice);
        OnPropertyChanged(nameof(Header));
    }
}

public sealed class OfflineResourcePickerViewModel : ObservableObject
{
    private readonly OfflineResourceFolder[] _folders;
    private readonly Dictionary<OfflineResourceFolder, bool> _beforeSearch = [];
    private string _searchText = string.Empty;
    private OfflineResourceChoice? _selectedChoice;
    public OfflineResourcePickerViewModel(IReadOnlyList<OfflineResourceChoice> resources, string title)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Title = string.IsNullOrWhiteSpace(title) ? "选择资源" : title.Trim();
        _folders = resources.GroupBy(choice => choice.SourceStoryId)
            .Select(group => new OfflineResourceFolder(group.First().SourceStoryName, group.ToArray())).ToArray();
        RefreshFilter();
    }
    public string Title { get; }
    public string Explanation => "展开故事文件夹，选择要引用的资源。";
    public ObservableCollection<OfflineResourceFolder> Folders { get; } = [];
    public bool HasMatches => Folders.Count != 0;
    public string EmptyText => _folders.Length == 0 ? "项目中没有可引用的资源。" : "没有匹配的资源。";
    public string SearchText
    {
        get => _searchText;
        set
        {
            var next = value ?? string.Empty;
            if (next.Trim().Length != 0 && _searchText.Trim().Length == 0)
                foreach (var folder in _folders) _beforeSearch[folder] = folder.IsExpanded;
            if (SetProperty(ref _searchText, next)) RefreshFilter();
        }
    }
    public OfflineResourceChoice? SelectedChoice
    {
        get => _selectedChoice;
        set { if (SetProperty(ref _selectedChoice, value)) OnPropertyChanged(nameof(CanConfirm)); }
    }
    public bool CanConfirm => SelectedChoice is not null;
    public void Select(OfflineResourceChoice? choice) => SelectedChoice = choice;
    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        Folders.Clear();
        foreach (var folder in _folders)
        {
            folder.Filter(query);
            if (query.Length != 0) folder.IsExpanded = true;
            else if (_beforeSearch.TryGetValue(folder, out var expanded)) folder.IsExpanded = expanded;
            if (folder.Matches.Count != 0) Folders.Add(folder);
        }
        if (query.Length == 0) _beforeSearch.Clear();
        if (SelectedChoice is not null && !Folders.Any(folder => folder.Matches.Contains(SelectedChoice))) SelectedChoice = null;
        OnPropertyChanged(nameof(HasMatches)); OnPropertyChanged(nameof(EmptyText));
    }
}
