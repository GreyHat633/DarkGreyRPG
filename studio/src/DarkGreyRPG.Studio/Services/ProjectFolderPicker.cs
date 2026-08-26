using Microsoft.Win32;

namespace DarkGreyRPG.Studio.Services;

public sealed class ProjectFolderPicker : IProjectFolderPicker
{
    public string? PickProjectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 DarkGrey RPG 项目目录",
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
