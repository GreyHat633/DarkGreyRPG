using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DarkGreyRPG.Studio.Services;

/// <summary>
/// Writes one UTF-8 crash report per failure next to the Studio settings.
/// Every filesystem operation is guarded so an unavailable log directory can
/// never terminate the application while it is handling another exception.
/// </summary>
public sealed class CrashLogService : ICrashLogService
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _applicationVersion;

    public CrashLogService(
        string? settingsPath = null,
        Func<DateTimeOffset>? clock = null,
        string? applicationVersion = null)
    {
        var effectiveSettingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DarkGreyRPG",
                "Studio",
                "settings.json")
            : settingsPath;

        try
        {
            var fullSettingsPath = Path.GetFullPath(effectiveSettingsPath);
            var settingsDirectory = Path.GetDirectoryName(fullSettingsPath);
            LogsDirectory = string.IsNullOrWhiteSpace(settingsDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "logs")
                : Path.Combine(settingsDirectory, "logs");
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException)
        {
            LogsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        }

        _clock = clock ?? (() => DateTimeOffset.Now);
        _applicationVersion = applicationVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";
    }

    public string LogsDirectory { get; }

    public string? Log(
        Exception exception,
        string context,
        IReadOnlyDictionary<string, string?>? details = null)
    {
        if (exception is null || string.IsNullOrWhiteSpace(context))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(LogsDirectory);
            var timestamp = _clock();
            var fileName = $"crash-{timestamp:yyyyMMdd-HHmmssfff}.log";
            var path = Path.Combine(LogsDirectory, fileName);
            var suffix = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(
                    LogsDirectory,
                    $"crash-{timestamp:yyyyMMdd-HHmmssfff}-{suffix++}.log");
            }

            var builder = new StringBuilder()
                .AppendLine("DarkGrey RPG Studio crash report")
                .Append("Timestamp (local): ").AppendLine(timestamp.ToString("O"))
                .Append("Studio Version: ").AppendLine(_applicationVersion)
                .Append("OS: ").AppendLine(RuntimeInformation.OSDescription)
                .Append("Context: ").AppendLine(context.Trim());

            if (details is not null)
            {
                foreach (var detail in details.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    builder.Append(detail.Key).Append(": ").AppendLine(detail.Value ?? "(none)");
                }
            }

            builder.AppendLine("Exception:")
                .AppendLine(exception.ToString());

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }
        catch (Exception)
        {
            // A crash reporter must not replace the original exception or crash
            // again when the settings directory is unavailable.
            return null;
        }
    }
}
