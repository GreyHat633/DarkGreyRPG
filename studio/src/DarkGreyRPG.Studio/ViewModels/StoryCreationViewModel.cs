using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Name entry for a current Story whose immutable UID is already allocated.</summary>
public sealed class StoryCreationViewModel : ObservableObject
{
    private string _displayName = string.Empty;
    public StoryCreationViewModel(string allocatedStoryUid) => StoryUid = Core.Identity.StoryUid.Parse(allocatedStoryUid);
    public StoryUid StoryUid { get; }
    public string Title => "新建 Story";
    public string Description => "在当前项目中新建一个独立故事。";
    public string ActionText => "创建";
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
