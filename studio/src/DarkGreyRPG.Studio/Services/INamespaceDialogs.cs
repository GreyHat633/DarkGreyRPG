using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Services;

public interface INamespaceDialogs
{
    string? RequestNamespace(string? currentNamespace, bool firstUse);

    bool ConfirmMigration(NamespaceMigrationPreview preview);

}

/// <summary>Non-interactive default used when the shell is hosted without WPF dialogs.</summary>
public sealed class NullNamespaceDialogs : INamespaceDialogs
{
    public string? RequestNamespace(string? currentNamespace, bool firstUse) => null;

    public bool ConfirmMigration(NamespaceMigrationPreview preview) => false;
}
