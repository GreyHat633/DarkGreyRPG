namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Edits only a resource display name; stable identity is read-only context.</summary>
public sealed class DisplayNameDialogViewModel : ObservableObject
{
    private string _displayName;

    public DisplayNameDialogViewModel(string resourceLabel, string id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(resourceLabel)) throw new ArgumentException("Resource label is required.", nameof(resourceLabel));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Resource ID is required.", nameof(id));
        ResourceLabel = resourceLabel;
        Id = id;
        _displayName = displayName ?? string.Empty;
    }

    public string ResourceLabel { get; }
    public string Id { get; }
    public string Title => $"重命名{ResourceLabel}";
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

    public string ValidationText => string.IsNullOrWhiteSpace(DisplayName) ? "显示名称不能为空。" : string.Empty;
    public bool CanConfirm => ValidationText.Length == 0;
}
