using System.Windows.Controls;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class WorkspaceCloseDialogTests
{
    [STATestMethod]
    public void PresentsExactWorkspaceLevelSaveDiscardCancelActions()
    {
        var dialog = new WorkspaceCloseDialog();

        Assert.AreEqual("保存", ((Button)dialog.FindName("SaveButton")).Content);
        Assert.AreEqual("不保存", ((Button)dialog.FindName("DiscardButton")).Content);
        Assert.AreEqual("取消", ((Button)dialog.FindName("CancelButton")).Content);
    }
}
