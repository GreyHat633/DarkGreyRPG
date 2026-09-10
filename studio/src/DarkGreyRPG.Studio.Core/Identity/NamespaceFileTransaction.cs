using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>A detached file replacement or deletion in a project-relative path.</summary>
public record NamespaceFileChange(string RelativePath, byte[]? ExpectedBytes, byte[]? DesiredBytes);

/// <summary>
/// Applies a validated set of namespace file changes as one in-process transaction.
/// This provides all-or-rollback behavior for ordinary failures while the process is
/// running; it does not provide crash-durable atomicity.
/// </summary>
public sealed class NamespaceFileTransaction
{
    private static readonly ConcurrentDictionary<string, object> ProjectLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string, byte[]>? _writeFile;
    private readonly Action<string>? _deleteFile;

    public NamespaceFileTransaction(
        Action<string, byte[]>? writeFile = null,
        Action<string>? deleteFile = null)
    {
        _writeFile = writeFile;
        _deleteFile = deleteFile;
    }

    /// <summary>
    /// Validates the original files, validates the final typed project, then applies
    /// every write followed by every delete. Any application failure restores the
    /// exact original bytes or absence for every target.
    /// </summary>
    public void Apply(
        string projectDirectory,
        IReadOnlyList<NamespaceFileChange> changes,
        Action validateFinalProject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(validateFinalProject);

        var project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectDirectory));
        EnsureProjectDirectory(project);

        var lockKey = TrimDirectorySeparator(project);
        var projectLock = ProjectLocks.GetOrAdd(lockKey, static _ => new object());
        lock (projectLock)
        {
            var targets = PrepareTargets(project, changes);
            ValidateExpected(project, targets);
            validateFinalProject();
            ValidateExpected(project, targets);

            var createdDirectories = new List<string>();
            try
            {
                foreach (var target in targets.Where(target => target.DesiredBytes is not null))
                {
                    EnsureParentDirectory(target.AbsolutePath, createdDirectories);
                    Write(target.AbsolutePath, target.DesiredBytes!);
                }

                foreach (var target in targets.Where(target => target.DesiredBytes is null))
                {
                    Delete(target.AbsolutePath);
                }
            }
            catch (Exception applyException)
            {
                var rollbackFailures = Restore(targets, createdDirectories);
                if (rollbackFailures.Count != 0)
                {
                    throw new NamespaceFileTransactionException(applyException, rollbackFailures);
                }

                ExceptionDispatchInfo.Capture(applyException).Throw();
                throw;
            }
        }
    }

    private void Write(string path, byte[] bytes)
    {
        if (_writeFile is not null)
        {
            _writeFile(path, bytes.ToArray());
            return;
        }

        AtomicWrite(path, bytes);
    }

    private void Delete(string path)
    {
        if (_deleteFile is not null)
        {
            _deleteFile(path);
            return;
        }

        File.Delete(path);
    }

    private static List<Exception> Restore(
        IReadOnlyList<Target> targets,
        IReadOnlyList<string> createdDirectories)
    {
        var failures = new List<Exception>();
        foreach (var target in targets)
        {
            try
            {
                if (target.OriginalBytes is null)
                {
                    if (File.Exists(target.AbsolutePath))
                    {
                        File.Delete(target.AbsolutePath);
                    }
                    else if (Directory.Exists(target.AbsolutePath))
                    {
                        throw new IOException($"Rollback target is now a directory: '{target.RelativePath}'.");
                    }
                }
                else
                {
                    if (Directory.Exists(target.AbsolutePath))
                    {
                        throw new IOException($"Rollback target is now a directory: '{target.RelativePath}'.");
                    }

                    var parent = Path.GetDirectoryName(target.AbsolutePath)
                        ?? throw new IOException($"Rollback target has no parent: '{target.RelativePath}'.");
                    Directory.CreateDirectory(parent);
                    AtomicWrite(target.AbsolutePath, target.OriginalBytes);
                }
            }
            catch (Exception exception)
            {
                failures.Add(new IOException($"Rollback failed for '{target.RelativePath}'.", exception));
            }
        }

        foreach (var directory in createdDirectories
                     .OrderByDescending(path => path.Length)
                     .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(directory)
                    && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception exception)
            {
                failures.Add(new IOException($"Rollback directory cleanup failed for '{directory}'.", exception));
            }
        }

        return failures;
    }

    private static List<Target> PrepareTargets(
        string project,
        IReadOnlyList<NamespaceFileChange> changes)
    {
        var targets = new List<Target>(changes.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            var relative = NormalizeRelativePath(change.RelativePath);
            if (!seen.Add(relative))
            {
                throw new ArgumentException($"Duplicate project-relative path alias: '{change.RelativePath}'.", nameof(changes));
            }

            var absolute = Path.GetFullPath(Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar)));
            var projectPrefix = project.EndsWith(Path.DirectorySeparatorChar)
                ? project
                : project + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Path escapes the project directory: '{change.RelativePath}'.", nameof(changes));
            }

            EnsureNoReparsePoints(project, absolute, relative);
            if (Directory.Exists(absolute))
            {
                throw new IOException($"Transaction target is a directory: '{relative}'.");
            }

            targets.Add(new Target(
                relative,
                absolute,
                change.ExpectedBytes?.ToArray(),
                change.DesiredBytes?.ToArray()));
        }

        return targets;
    }

    private static void ValidateExpected(string project, IReadOnlyList<Target> targets)
    {
        foreach (var target in targets)
        {
            EnsureNoReparsePoints(project, target.AbsolutePath, target.RelativePath);

            var exists = File.Exists(target.AbsolutePath);
            if (target.OriginalBytes is null)
            {
                if (exists || Directory.Exists(target.AbsolutePath))
                {
                    throw new InvalidOperationException($"Expected '{target.RelativePath}' to be absent.");
                }
            }
            else if (!exists || !File.ReadAllBytes(target.AbsolutePath).AsSpan().SequenceEqual(target.OriginalBytes))
            {
                throw new InvalidOperationException($"Expected bytes for '{target.RelativePath}' are stale.");
            }
        }
    }

    private static void EnsureParentDirectory(string path, ICollection<string> createdDirectories)
    {
        var parent = Path.GetDirectoryName(path)
            ?? throw new IOException($"Target has no parent: '{path}'.");
        var missing = new Stack<string>();
        var current = parent;
        while (!Directory.Exists(current))
        {
            if (File.Exists(current))
            {
                throw new IOException($"Parent path is a file: '{current}'.");
            }

            missing.Push(current);
            current = Path.GetDirectoryName(current)
                ?? throw new IOException($"Unable to find a project directory for '{path}'.");
        }

        while (missing.Count > 0)
        {
            var directory = missing.Pop();
            Directory.CreateDirectory(directory);
            createdDirectories.Add(directory);
        }
    }

    private static void EnsureProjectDirectory(string project)
    {
        if (!Directory.Exists(project))
        {
            throw new DirectoryNotFoundException($"Project directory does not exist: '{project}'.");
        }

        if ((File.GetAttributes(project) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Project directory cannot be a reparse point: '{project}'.");
        }
    }

    private static void EnsureNoReparsePoints(string project, string absolute, string relative)
    {
        var current = absolute;
        while (current.Length >= project.Length)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Path contains a reparse point: '{relative}'.");
                }
            }

            if (string.Equals(current, project, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            current = Path.GetDirectoryName(current)
                ?? throw new IOException($"Unable to anchor path '{relative}' in the project directory.");
        }

        throw new IOException($"Path is not anchored in the project directory: '{relative}'.");
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
        {
            throw new ArgumentException($"Path must be relative and cannot contain a colon: '{relativePath}'.", nameof(relativePath));
        }

        var segments = relativePath.Replace('\\', '/').Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException($"Path contains an empty or traversal segment: '{relativePath}'.", nameof(relativePath));
        }

        foreach (var segment in segments)
        {
            var deviceName = segment.Split('.')[0].ToUpperInvariant();
            var reservedDevice = deviceName is "CON" or "PRN" or "AUX" or "NUL"
                || deviceName.Length == 4 && deviceName[3] is >= '1' and <= '9'
                && (deviceName.StartsWith("COM", StringComparison.Ordinal) || deviceName.StartsWith("LPT", StringComparison.Ordinal));
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || segment.EndsWith('.') || segment.EndsWith(' ') || reservedDevice)
            {
                throw new ArgumentException($"Path contains an invalid filename segment: '{relativePath}'.", nameof(relativePath));
            }
        }

        return string.Join('/', segments);
    }

    private static void AtomicWrite(string destination, byte[] bytes)
    {
        var parent = Path.GetDirectoryName(destination)
            ?? throw new IOException($"Destination has no parent: '{destination}'.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".dgr-namespace-transaction-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(destination))
            {
                File.Replace(temporary, destination, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporary, destination);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string TrimDirectorySeparator(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record Target(
        string RelativePath,
        string AbsolutePath,
        byte[]? OriginalBytes,
        byte[]? DesiredBytes);
}

public sealed class NamespaceFileTransactionException : IOException
{
    public NamespaceFileTransactionException(Exception applyException, IReadOnlyList<Exception> rollbackFailures)
        : base(
            "Namespace file transaction failed and rollback also failed: "
            + string.Join(" | ", rollbackFailures.Select(failure => failure.Message)),
            new AggregateException(applyException, new AggregateException(rollbackFailures)))
    {
        ApplyException = applyException;
        RollbackFailures = new ReadOnlyCollection<Exception>(rollbackFailures.ToArray());
    }

    public Exception ApplyException { get; }

    public IReadOnlyList<Exception> RollbackFailures { get; }
}
