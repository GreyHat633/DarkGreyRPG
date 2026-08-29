using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Presentation model for the explicit, read-only migration preview.</summary>
public sealed class CanonicalProjectMigrationDialogViewModel : ObservableObject
{
    public CanonicalProjectMigrationDialogViewModel(CanonicalProjectMigrationPreviewResult preview)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        Issues = new ObservableCollection<CanonicalProjectMigrationIssueViewModel>(
            preview.Issues.Select(issue => new CanonicalProjectMigrationIssueViewModel(issue)));
        ProposedDestinations = new ObservableCollection<string>(
            preview.ProposedWrites.Select(write => write.RelativePath));
        ConfirmCommand = new RelayCommand(() => Confirmed?.Invoke(), () => CanApply);
        CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
    }

    public CanonicalProjectMigrationPreviewResult Preview { get; }
    public string ProjectDirectory => Preview.ProjectDirectory;
    public bool CanApply => Preview.CanApply;
    public int SourceCount => Preview.SourceFingerprints.Count;
    public int ResourceCount => Preview.ResourcePreviews.Count;
    public int WriteCount => Preview.ProposedWrites.Count;
    public int IssueCount => Issues.Count;
    public ObservableCollection<CanonicalProjectMigrationIssueViewModel> Issues { get; }
    public ObservableCollection<string> ProposedDestinations { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Raised only by an explicit confirmation action.</summary>
    public Action? Confirmed { get; set; }
    public Action? Cancelled { get; set; }

    public string ApplyStateText => CanApply
        ? "预览通过检查，可以应用。"
        : "预览包含错误，无法应用。";

    public string IssuesText => IssueCount == 0 ? "无" : $"{IssueCount} 项";

    public void RefreshCommandState() => ConfirmCommand.RaiseCanExecuteChanged();
}

public sealed class CanonicalProjectMigrationIssueViewModel
{
    public CanonicalProjectMigrationIssueViewModel(ValidationIssue issue, string? source = null)
    {
        Code = issue?.Code ?? throw new ArgumentNullException(nameof(issue));
        Message = issue.Message;
        Severity = issue.Severity;
        Source = source ?? issue.Field ?? string.Empty;
    }

    public string Code { get; }
    public string Message { get; }
    public ValidationSeverity Severity { get; }
    public string Source { get; }
    public string DisplayText => string.IsNullOrWhiteSpace(Source)
        ? $"[{Code}] {Message}"
        : $"[{Code}] {Message} ({Source})";
}
