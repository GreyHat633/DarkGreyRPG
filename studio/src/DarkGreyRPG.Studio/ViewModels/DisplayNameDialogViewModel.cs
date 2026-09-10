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
        AllowIdentityEdit = allowIdentityEdit;
        _localId = DgrResourceId.LocalId(id);
        _displayName = displayName ?? string.Empty;
        _tagsText = string.Join("、", tags ?? []);
    }

    private string _localId = string.Empty;
    public bool AllowIdentityEdit { get; }
    public string NamespacePrefix => Id.Contains(':') ? Id[..(Id.IndexOf(':') + 1)] : string.Empty;
    public string NewId => NamespacePrefix + LocalId.Trim();
    public string LocalId
    {
        get => _localId;
        set { if (SetProperty(ref _localId, value ?? string.Empty)) { OnPropertyChanged(nameof(ValidationText)); OnPropertyChanged(nameof(CanConfirm)); } }
    }
    public string ResourceLabel { get; }
    public string Id { get; }
    public string Title => $"编辑{ResourceLabel}";
    public string IdentityText => $"{ResourceLabel} ID：{Id}";

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

    public string ValidationText => string.IsNullOrWhiteSpace(DisplayName) ? "显示名称不能为空。" : AllowIdentityEdit && NewId != Id && !DgrResourceId.IsFullId(NewId) ? "资源 ID 格式无效。" : string.Empty;
    public bool CanConfirm => ValidationText.Length == 0;
}
