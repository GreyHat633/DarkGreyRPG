using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Items;

public sealed class ItemValidationException : Exception
{
    public ItemValidationException(IEnumerable<ValidationIssue> issues)
        : base(CreateMessage(issues))
    {
        Issues = [.. issues];
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    private static string CreateMessage(IEnumerable<ValidationIssue> issues)
    {
        var errors = issues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        return errors.Length == 0 ? "Item validation failed." : string.Join(" ", errors.Select(issue => issue.Message));
    }
}

public sealed class ItemDataException : Exception
{
    public ItemDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
