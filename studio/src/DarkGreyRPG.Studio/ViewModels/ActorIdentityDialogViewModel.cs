using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ActorIdentityDialogViewModel : ObservableObject
{
    private string _id;
    private string _displayName;

    private ActorIdentityDialogViewModel(
        string title,
        string actionText,
        string description,
        string id,
        string displayName,
        bool isDisplayNameVisible)
    {
        Title = title;
        ActionText = actionText;
        Description = description;
        _id = id;
        _displayName = displayName;
        IsDisplayNameVisible = isDisplayNameVisible;
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public string Title { get; }

    public string ActionText { get; }

    public string Description { get; }

    public bool IsDisplayNameVisible { get; }

    public RelayCommand ApplySuggestionCommand { get; }

    public string EditableId { get => Id; set { } }

    public string Id
    {
        get => _id;
        set
        {
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value ?? string.Empty))
            {
                return;
            }

            RaiseValidationProperties();
        }
    }

    public string NormalizedSuggestion => Id;

    public bool HasSuggestion =>
        NormalizedSuggestion.Length > 0 &&
        !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            var messages = new List<string>();
            if (!ResourceAddress.IsKey(Id) || ResourceAddress.FromKey(Id).Kind != ResourceKind.Actor)
                messages.Add("资源内部地址无效。");
            if (IsDisplayNameVisible && string.IsNullOrWhiteSpace(DisplayName))
            {
                messages.Add("资源名称不能为空。");
            }

            return string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanConfirm => ValidationText.Length == 0;

    public static ActorIdentityDialogViewModel ForCreate(string suggestedId) =>
        new("新建角色", "创建", "创建独立的新角色，并归入当前故事。", suggestedId, "新角色", isDisplayNameVisible: true);

    public static ActorIdentityDialogViewModel ForImport(
        string sourceDisplayName,
        string suggestedId) =>
        new(
            "导入角色副本",
            "创建副本",
            $"将以“{sourceDisplayName}”为模板创建新的独立资源。后续修改不会影响原角色。",
            suggestedId,
            sourceDisplayName,
            isDisplayNameVisible: true);

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private void RaiseValidationProperties()
    {
        OnPropertyChanged(nameof(EditableId));
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}
