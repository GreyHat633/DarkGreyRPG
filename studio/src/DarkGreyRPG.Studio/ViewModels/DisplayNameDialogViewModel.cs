using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Edits resource identity and metadata.</summary>
public sealed class DisplayNameDialogViewModel : ObservableObject
{
    private string _displayName;

    public DisplayNameDialogViewModel(string resourceLabel, string id, string displayName, bool allowIdentityEdit = false, IReadOnlyList<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(resourceLabel)) throw new ArgumentException("Resource label is required.", nameof(resourceLabel));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Resource ID is required.", nameof(id));
        ResourceLabel = resourceLabel;
        Id = id;
        AllowIdentityEdit = false;
        _localId = string.Empty;
        _displayName = displayName ?? string.Empty;
        _tagsText = string.Join("、", tags ?? []);
    }

    private string _localId = string.Empty;
    public bool AllowIdentityEdit { get; }
    public string NewId => Id;
    public string LocalId
    {
        get => _localId;
        set { if (SetProperty(ref _localId, value ?? string.Empty)) { OnPropertyChanged(nameof(ValidationText)); OnPropertyChanged(nameof(CanConfirm)); } }
    }
    public string ResourceLabel { get; }
    public string NameLabel => ResourceLabel.Contains("角色") || ResourceLabel.Contains("物品") ? "资源名称" : "显示名称";
    public string Id { get; }
    public string Title => $"编辑{ResourceLabel}";
    public string IdentityText => StoryUid.IsValid(Id) ? $"Story UID：{Id}" : string.Empty;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(ValidationText));
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private string _tagsText = string.Empty;
    public string TagsText { get => _tagsText; set => SetProperty(ref _tagsText, value ?? string.Empty); }
    public IReadOnlyList<string> Tags => ResourceTagsInput.Parse(TagsText);

    public string ValidationText => string.IsNullOrWhiteSpace(DisplayName) ? $"{NameLabel}不能为空。" : string.Empty;
    public bool CanConfirm => ValidationText.Length == 0;
}
