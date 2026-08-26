namespace DarkGreyRPG.Studio.Core.Validation;

public enum ValidationSeverity
{
    Warning,
    Error,
}

public sealed record ValidationIssue(
    string Code,
    string Message,
    string? Field = null,
    ValidationSeverity Severity = ValidationSeverity.Error);
