using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public sealed class ActorValidationException : Exception
{
    public ActorValidationException(IEnumerable<ValidationIssue> issues)
        : base(CreateMessage(issues))
    {
        Issues = [.. issues];
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    private static string CreateMessage(IEnumerable<ValidationIssue> issues)
    {
        var errors = issues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        return errors.Length == 0
            ? "Actor validation failed."
            : string.Join(" ", errors.Select(issue => issue.Message));
    }
}
