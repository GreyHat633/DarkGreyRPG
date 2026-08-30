using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class CanonicalActorCreationChoiceViewModel : ObservableObject
{
    private CanonicalStoryActorKind _selectedKind = CanonicalStoryActorKind.Individual;

    public CanonicalActorCreationChoiceViewModel(string storyDisplayName)
        => StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName.Trim();

    public string StoryDisplayName { get; }
    public CanonicalStoryActorKind SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (!Enum.IsDefined(value) || !SetProperty(ref _selectedKind, value)) return;
            OnPropertyChanged(nameof(IsIndividual));
            OnPropertyChanged(nameof(IsCollective));
        }
    }
    public bool IsIndividual { get => SelectedKind == CanonicalStoryActorKind.Individual; set { if (value) SelectedKind = CanonicalStoryActorKind.Individual; } }
    public bool IsCollective { get => SelectedKind == CanonicalStoryActorKind.Collective; set { if (value) SelectedKind = CanonicalStoryActorKind.Collective; } }
}

public sealed class CanonicalActorIdentityDialogViewModel : ObservableObject
{
    private string _id;
    private string _displayName;
    private string _tagsText = string.Empty;

    public CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind kind, string suggestedId)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        _id = suggestedId ?? string.Empty;
        _displayName = kind == CanonicalStoryActorKind.Individual ? "新角色" : "新角色组";
        ApplySuggestionCommand = new RelayCommand(() => Id = NormalizedSuggestion, () => HasSuggestion);
    }

    public CanonicalStoryActorKind Kind { get; }
    public string Title => Kind == CanonicalStoryActorKind.Individual ? "新建个体角色" : "新建集体角色";
    public string IdentityLabel => Kind == CanonicalStoryActorKind.Individual ? "NPC ID" : "Group ID";
    public string Description => Kind == CanonicalStoryActorKind.Individual
        ? "创建一个以 NPC ID 标识的个体角色。"
        : "创建一个以 Group ID 标识的集体角色。";
    public RelayCommand ApplySuggestionCommand { get; }
    public string Id { get => _id; set { if (SetProperty(ref _id, value ?? string.Empty)) RaiseValidation(); } }
    public string DisplayName { get => _displayName; set { if (SetProperty(ref _displayName, value ?? string.Empty)) RaiseValidation(); } }
    public string TagsText { get => _tagsText; set { if (SetProperty(ref _tagsText, value ?? string.Empty)) RaiseValidation(); } }
    public IReadOnlyList<string> Tags => TagsText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.Trim()).ToArray();
    public string NormalizedSuggestion => ActorValidator.NormalizeId(Id);
    public bool HasSuggestion => NormalizedSuggestion.Length > 0 && !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);
    public string ValidationText
    {
        get
        {
            var errors = ActorValidator.ValidateId(Id, ActorIdPolicy.NewResource)
                .Where(issue => issue.Severity == ValidationSeverity.Error).Select(issue => issue.Message).ToList();
            if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add("显示名称不能为空。");
            if (Tags.Count != Tags.Distinct(StringComparer.Ordinal).Count()) errors.Add("标签不能重复。");
            return string.Join(Environment.NewLine, errors);
        }
    }
    public bool CanConfirm => ValidationText.Length == 0;

    private void RaiseValidation()
    {
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}
