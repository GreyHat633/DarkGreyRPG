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

    public void ReplaceForSource(string source, IEnumerable<ProblemItem> problems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(problems);

        var replacement = problems
            .Select(problem =>
            {
                ArgumentNullException.ThrowIfNull(problem);
                return problem with { Source = source };
            })
            .ToArray();

        RemoveSource(source);
        for (var index = replacement.Length - 1; index >= 0; index--)
        {
            Problems.Insert(0, replacement[index]);
        }
    }

    public void ReplaceForSourceTree(string rootSource, IEnumerable<ProblemItem> problems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSource);
        ArgumentNullException.ThrowIfNull(problems);

        var prefix = rootSource + "/";
        var replacement = problems.Select(problem =>
        {
            ArgumentNullException.ThrowIfNull(problem);
            var source = string.IsNullOrWhiteSpace(problem.Source) ? rootSource : problem.Source;
            if (!string.Equals(source, rootSource, StringComparison.Ordinal) &&
                !source.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Problem source must be inside the requested source tree.", nameof(problems));
            return problem with { Source = source };
        }).ToArray();

        RemoveSourceTree(rootSource);
        for (var index = replacement.Length - 1; index >= 0; index--) Problems.Insert(0, replacement[index]);
    }

    public void RemoveSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        for (var index = Problems.Count - 1; index >= 0; index--)
        {
            if (string.Equals(Problems[index].Source, source, StringComparison.Ordinal))
            {
                Problems.RemoveAt(index);
            }
        }
    }

    public void RemoveSourceTree(string rootSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSource);
        var prefix = rootSource + "/";
        for (var index = Problems.Count - 1; index >= 0; index--)
            if (string.Equals(Problems[index].Source, rootSource, StringComparison.Ordinal) ||
                Problems[index].Source?.StartsWith(prefix, StringComparison.Ordinal) == true)
                Problems.RemoveAt(index);
    }

    public void ClearAll() => Problems.Clear();

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

    public void Clear() => ClearAll();

    private void OnProblemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(HasErrors));
    }
}
