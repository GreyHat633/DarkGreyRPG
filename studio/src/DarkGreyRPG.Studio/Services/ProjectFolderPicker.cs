using Microsoft.Win32;

namespace DarkGreyRPG.Studio.Services;

public sealed class ProjectFolderPicker : IProjectFolderPicker
{
    public string? PickProjectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "打开项目（外部项目将复制到本地）",
            InitialDirectory = StudioStoragePaths.Default.Projects,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
