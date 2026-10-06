using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
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
        _displayName = string.Empty;
        ApplySuggestionCommand = new RelayCommand(() => Id = NormalizedSuggestion, () => HasSuggestion);
    }

    public CanonicalStoryActorKind Kind { get; }
    public string Title => Kind == CanonicalStoryActorKind.Individual ? "新建角色" : "新建角色组";
    public string EditableId { get => Id; set { } }

    public string IdentityLabel => "角色";
    public string Description => Kind == CanonicalStoryActorKind.Individual ? "创建角色，并归入当前故事。" : "创建角色组，并归入当前故事。";
    public RelayCommand ApplySuggestionCommand { get; }
    public string Id { get => _id; set { } }
    public string DisplayName { get => _displayName; set { if (SetProperty(ref _displayName, value ?? string.Empty)) RaiseValidation(); } }
    public string TagsText { get => _tagsText; set { if (SetProperty(ref _tagsText, value ?? string.Empty)) RaiseValidation(); } }
    public IReadOnlyList<string> Tags => TagsText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.Trim()).ToArray();
    public string NormalizedSuggestion => Id;
    public bool HasSuggestion => false;

    public string ValidationText
    {
        get
        {
            var errors = new List<string>();
            if (!ResourceAddress.IsKey(Id) || ResourceAddress.FromKey(Id).Kind != (Core.Identity.ResourceKind.Actor))
                errors.Add("资源内部地址无效。");
            if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add("资源名称不能为空。");
            if (Tags.Count != Tags.Distinct(StringComparer.Ordinal).Count()) errors.Add("标签不能重复。");
            return string.Join(Environment.NewLine, errors);
        }
    }
    public bool CanConfirm => ValidationText.Length == 0;


    private void RaiseValidation()
    {
        OnPropertyChanged(nameof(EditableId));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}
