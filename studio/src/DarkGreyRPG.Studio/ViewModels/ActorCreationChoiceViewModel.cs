using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ActorCreationChoiceViewModel : ObservableObject
{
    private ActorCreationMode _selectedMode = ActorCreationMode.Blank;

    public ActorCreationChoiceViewModel(string storyDisplayName)
    {
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前剧情" : storyDisplayName;
    }

    public string StoryDisplayName { get; }

    public ActorCreationMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!SetProperty(ref _selectedMode, value)) return;
            OnPropertyChanged(nameof(IsBlank));
            OnPropertyChanged(nameof(IsImportAsNew));
        }
    }

    public bool IsBlank
    {
        get => SelectedMode == ActorCreationMode.Blank;
        set { if (value) SelectedMode = ActorCreationMode.Blank; }
    }

    public bool IsImportAsNew
    {
        get => SelectedMode == ActorCreationMode.ImportAsNew;
        set { if (value) SelectedMode = ActorCreationMode.ImportAsNew; }
    }
}
