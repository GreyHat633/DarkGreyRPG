using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record ProblemItem(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Field = null,
    string? Source = null)
{
    public bool IsError => Severity == ValidationSeverity.Error;

    public bool IsWarning => Severity == ValidationSeverity.Warning;
}

public sealed class ProblemsViewModel : ObservableObject
{
    public ProblemsViewModel()
    {
        Problems.CollectionChanged += OnProblemsChanged;
    }

    public ObservableCollection<ProblemItem> Problems { get; } = [];

    public int Count => Problems.Count;

    public int ErrorCount => Problems.Count(problem => problem.IsError);

    public int WarningCount => Problems.Count(problem => problem.IsWarning);

    public bool HasProblems => Problems.Count > 0;

    public bool HasErrors => ErrorCount > 0;

    public void ReplaceFromValidationIssues(
        IEnumerable<ValidationIssue> issues,
        string? source = null)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Problems.Clear();
        foreach (var issue in issues)
        {
            ArgumentNullException.ThrowIfNull(issue);
            Problems.Add(new ProblemItem(
                issue.Severity,
                issue.Code,
                issue.Message,
                issue.Field,
                source));
        }
    }

    public void Replace(IEnumerable<ProblemItem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        Problems.Clear();
        foreach (var problem in problems)
        {
            ArgumentNullException.ThrowIfNull(problem);
            Problems.Add(problem);
        }
    }

    public void Clear() => Problems.Clear();

    private void OnProblemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(HasErrors));
    }
}
