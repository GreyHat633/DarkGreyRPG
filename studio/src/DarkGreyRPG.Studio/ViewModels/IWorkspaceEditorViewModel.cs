using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public interface IWorkspaceEditorViewModel
{
    string Id { get; }
    bool IsDirty { get; }
    bool CanSave { get; }
    string SaveStateText { get; }
    string ValidationText { get; }
    IReadOnlyList<ValidationIssue> ValidationIssues { get; }
    RelayCommand UndoCommand { get; }
    RelayCommand RedoCommand { get; }
}
