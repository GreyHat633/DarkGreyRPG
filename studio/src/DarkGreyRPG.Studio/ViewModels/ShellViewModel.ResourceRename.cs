using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    private void RenameCanonicalResourceIdentity(DgrResourceKind kind, string id, ResourceRenameRequest rename)
        => ReportWarning("资源身份固定，只能修改名称和标签。", "编辑资源");
}
