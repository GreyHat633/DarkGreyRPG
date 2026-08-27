namespace DarkGreyRPG.Studio.Services;

/// <summary>
/// Persists diagnostic information for UI failures without allowing logging to
/// become a second failure.
/// </summary>
public interface ICrashLogService
{
    string LogsDirectory { get; }

    string? Log(
        Exception exception,
        string context,
        IReadOnlyDictionary<string, string?>? details = null);
}
