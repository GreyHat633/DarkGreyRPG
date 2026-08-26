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

    public string Id
    {
        get => _id;
        set
        {
            if (!SetProperty(ref _id, value ?? string.Empty))
            {
                return;
            }

            RaiseValidationProperties();
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

    public string NormalizedSuggestion => ActorValidator.NormalizeId(Id);

    public bool HasSuggestion =>
        NormalizedSuggestion.Length > 0 &&
        !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            var messages = ActorValidator.ValidateId(Id, ActorIdPolicy.NewResource)
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Select(issue => issue.Message)
                .ToList();
            if (IsDisplayNameVisible && string.IsNullOrWhiteSpace(DisplayName))
            {
                messages.Add("显示名称不能为空。");
            }

            return string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanConfirm => ValidationText.Length == 0;

    public static ActorIdentityDialogViewModel ForCreate(string suggestedId) =>
        new("新建 Actor", "创建", "创建独立的新角色，并归入当前剧情。", suggestedId, "新角色", isDisplayNameVisible: true);

    public static ActorIdentityDialogViewModel ForImport(
        string sourceDisplayName,
        string suggestedId) =>
        new(
            "导入 Actor 副本",
            "创建副本",
            $"将以“{sourceDisplayName}”为模板创建新的独立资源。后续修改不会影响原角色。",
            suggestedId,
            sourceDisplayName,
            isDisplayNameVisible: true);

    public static ActorIdentityDialogViewModel ForRename(string currentId, string suggestedId) =>
        new("重命名 Actor", "重命名", $"为 Actor '{currentId}' 指定新的资源 ID。", suggestedId, currentId, isDisplayNameVisible: false);

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private void RaiseValidationProperties()
    {
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}
