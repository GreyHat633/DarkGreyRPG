using System.Windows;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Services;

public sealed class NamespaceDialogs(Func<Window?> ownerProvider) : INamespaceDialogs
{
    public string? RequestNamespace(string? currentNamespace, bool firstUse)
    {
        var dialog = new NamespaceDialog(currentNamespace, firstUse) { Owner = ownerProvider() };
        return dialog.ShowDialog() == true ? dialog.NamespaceValue : null;
    }

    public bool ConfirmMigration(NamespaceMigrationPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return MessageBox.Show(
            ownerProvider(),
            $"项目路径：{preview.ProjectDirectory}\n"
                + $"项目 NameSpace：{preview.GlobalNamespace}\n"
                + $"变更文件数：{preview.ChangedFileCount}\n"
                + $"资源重命名数：{preview.Renames.Count}\n\n"
                + "这会迁移所选范围内的故事与所属资源，并同步更新资源引用和编辑器布局。\n"
                + "外部引用资源自身的标识保持不变。\n"
                + "确认继续迁移吗？",
            "确认 NameSpace 迁移",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

}
