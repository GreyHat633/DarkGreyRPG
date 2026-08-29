using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Migration;

/// <summary>Stable, machine-readable failures from the explicit project migration transaction.</summary>
public sealed class CanonicalProjectMigrationException : Exception
{
    public CanonicalProjectMigrationException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException) => Code = code;

    public string Code { get; }
}

/// <summary>A detached record of the files created by a successful migration.</summary>
public sealed class CanonicalProjectMigrationTransactionResult
{
    internal CanonicalProjectMigrationTransactionResult(
        string project, string backup, IEnumerable<string> writtenPaths,
        IEnumerable<KeyValuePair<string, string>> writtenHashes)
    {
        Project = project;
        ProjectDirectory = project;
        FullProjectPath = project;
        Backup = backup;
        BackupPath = backup;
        WrittenPaths = new ReadOnlyCollection<string>(writtenPaths.ToArray());
        WrittenFiles = WrittenPaths;
        WrittenHashes = new ReadOnlyDictionary<string, string>(
            writtenHashes.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
        Hashes = WrittenHashes;
    }

    public string Project { get; }
    public string ProjectDirectory { get; }
    public string FullProjectPath { get; }
    public string Backup { get; }
    public string BackupPath { get; }
    public IReadOnlyList<string> WrittenPaths { get; }
    public IReadOnlyList<string> WrittenFiles { get; }
    public IReadOnlyDictionary<string, string> WrittenHashes { get; }
    public IReadOnlyDictionary<string, string> Hashes { get; }
}

/// <summary>
/// Explicit, fail-closed application of a canonical project migration preview.
/// Constructing this service and inspecting a preview are always read-only.
/// </summary>
public sealed class CanonicalProjectMigrationTransaction
{
    private readonly IAtomicFileWriter _writer;
    private readonly Func<DateTimeOffset> _utcNow;
    private int _running;

    public CanonicalProjectMigrationTransaction(
        IAtomicFileWriter? writer = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _writer = writer ?? new AtomicFileWriter();
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public CanonicalProjectMigrationTransaction(
        IAtomicFileWriter writer,
        Func<DateTime> utcNow)
        : this(writer, () => new DateTimeOffset(DateTime.SpecifyKind(utcNow(), DateTimeKind.Utc))) { }

    /// <summary>Applies exactly the supplied, previously inspected preview.</summary>
    public CanonicalProjectMigrationTransactionResult Apply(CanonicalProjectMigrationPreviewResult preview)
    {
        if (preview is null)
            throw Failure("migration.transaction.preview.invalid", "A migration preview is required.");
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw Failure("migration.transaction.busy", "Another canonical project migration is already running.");

        var backupExists = false;
        string? root = null;
        string? backupPath = null;
        var createdDirectories = new List<string>();
        var written = new List<string>();
        try
        {
            if (!preview.CanApply)
                throw Failure("migration.transaction.preview.invalid", "The migration preview is not applicable.");

            root = FullPath(preview.FullProjectPath, "migration.transaction.preview.project.invalid");
            var fresh = CanonicalProjectMigrationPreview.PreviewProject(root);
            VerifyFreshness(preview, fresh, root);

            var candidates = fresh.ProposedWrites.ToArray();
            ValidateCandidates(root, candidates);

            // A destination check is deliberately performed before the backup and
            // immediately before every write; this transaction never replaces data.
            EnsureDestinationsAbsent(candidates);

            backupPath = CreateBackup(root, fresh.SourceFingerprints, out backupExists);
            var afterBackup = CanonicalProjectMigrationPreview.PreviewProject(root);
            VerifyFreshness(fresh, afterBackup, root);
            candidates = afterBackup.ProposedWrites.ToArray();
            ValidateCandidates(root, candidates);
            EnsureDestinationsAbsent(candidates);

            foreach (var candidate in candidates)
            {
                var destination = FullPath(candidate.FullPath, "migration.transaction.destination.invalid");
                EnsureDestinationAbsent(destination);
                EnsureParent(destination, root, createdDirectories);
                written.Add(destination);
                WriteCandidate(candidate, destination);
            }

            var result = new CanonicalProjectMigrationTransactionResult(
                root, backupPath, written,
                written.Select(path => new KeyValuePair<string, string>(
                    path, HashBytes(File.ReadAllBytes(path)))));
            AppendLog(root, backupExists,
                $"{_utcNow():O} migration 2.1.3-to-0.3.0.0 succeeded; files={written.Count}; backup={backupPath}");
            return result;
        }
        catch (Exception primary)
        {
            Exception failure = primary;
            if (backupExists)
            {
                try { Rollback(written, createdDirectories); }
                catch (Exception rollback)
                {
                    failure = Failure("migration.transaction.rollback.failed",
                        "Canonical migration failed and rollback also failed.",
                        new AggregateException(primary, rollback));
                }
                if (root is not null)
                    AppendLog(root, true,
                        $"{_utcNow():O} migration 2.1.3-to-0.3.0.0 failed; rollback was attempted; backup={backupPath}; error={failure.Message}");
            }
            throw failure is CanonicalProjectMigrationException
                ? failure
                : Failure("migration.transaction.failed", "Canonical migration failed; rollback was attempted.", failure);
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private static void VerifyFreshness(
        CanonicalProjectMigrationPreviewResult supplied,
        CanonicalProjectMigrationPreviewResult fresh,
        string root)
    {
        if (!fresh.CanApply)
            throw Failure("migration.transaction.preview.stale", "The project changed after the preview and is no longer applicable.");
        if (!string.Equals(root, Path.GetFullPath(fresh.FullProjectPath), StringComparison.Ordinal))
            throw Failure("migration.transaction.preview.stale", "The project path changed after the preview.");

        static string Sources(CanonicalProjectMigrationPreviewResult value) => string.Join("\n",
            value.SourceFingerprints.OrderBy(x => x.RelativePath, StringComparer.Ordinal)
                .Select(x => $"{x.RelativePath}|{x.Sha256}|{x.Length}|{x.FullPath}"));
        static string Writes(CanonicalProjectMigrationPreviewResult value) => string.Join("\n",
            value.ProposedWrites.OrderBy(x => x.RelativePath, StringComparer.Ordinal)
                .Select(x => $"{x.RelativePath}|{x.Sha256}"));

        if (!string.Equals(Sources(supplied), Sources(fresh), StringComparison.Ordinal)
            || !string.Equals(Writes(supplied), Writes(fresh), StringComparison.Ordinal))
            throw Failure("migration.transaction.preview.stale",
                "The project sources or proposed canonical writes changed after the preview.");
    }

    private static void ValidateCandidates(string root, IReadOnlyList<CanonicalProjectMigrationWriteCandidate> candidates)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var path = FullPath(candidate.FullPath, "migration.transaction.destination.invalid");
            if (!paths.Add(path)) throw Failure("migration.transaction.destination.duplicate", $"Duplicate destination '{path}'.");
            if (!IsWithin(root, path))
                throw Failure("migration.transaction.destination.invalid", "A candidate destination escaped the project.");
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!string.Equals(relative, candidate.RelativePath, StringComparison.Ordinal))
                throw Failure("migration.transaction.destination.invalid", "A candidate destination is outside its preview path.");
            var bytes = Encoding.UTF8.GetBytes(candidate.Contents);
            if (!string.Equals(HashBytes(bytes), candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                throw Failure("migration.transaction.candidate.changed", $"Candidate '{candidate.RelativePath}' content changed.");
            try
            {
                if (candidate.IsMembership)
                {
                    var manifest = CanonicalStoryMembershipSerializer.Deserialize(candidate.Contents);
                    if (!string.Equals(manifest.StoryId, candidate.ResourceId, StringComparison.Ordinal))
                        throw Failure("migration.transaction.candidate.invalid", "Membership identity does not match its candidate.");
                }
                else
                {
                    var envelope = GraphResourceEnvelopeSerializer.Deserialize(candidate.Contents);
                    if (candidate.ResourceKind is null || envelope.ResourceKind != candidate.ResourceKind.Value
                        || !string.Equals(envelope.Id, candidate.ResourceId, StringComparison.Ordinal))
                        throw Failure("migration.transaction.candidate.invalid", "Envelope identity does not match its candidate.");
                }
            }
            catch (CanonicalProjectMigrationException) { throw; }
            catch (Exception exception)
            {
                throw Failure("migration.transaction.candidate.invalid", $"Candidate '{candidate.RelativePath}' is not strict canonical JSON.", exception);
            }
        }
    }

    private static void EnsureDestinationsAbsent(IEnumerable<CanonicalProjectMigrationWriteCandidate> candidates)
    {
        foreach (var candidate in candidates) EnsureDestinationAbsent(candidate.FullPath);
    }

    private static void EnsureDestinationAbsent(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
            throw Failure("migration.transaction.destination.exists", $"Canonical destination '{path}' already exists.");
    }

    private string CreateBackup(
        string root,
        IReadOnlyList<CanonicalProjectMigrationSourceFingerprint> sources,
        out bool exists)
    {
        exists = false;
        var parent = Path.Combine(root, ".migration-backups");
        var stamp = _utcNow().ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var path = Path.Combine(parent, stamp + "-0.3.0.0");
        for (var suffix = 1; Directory.Exists(path); suffix++)
            path = Path.Combine(parent, $"{stamp}-{suffix}-0.3.0.0");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(path);
        exists = true;
        try
        {
            foreach (var source in sources)
            {
                var sourcePath = FullPath(source.FullPath, "migration.transaction.source.invalid");
                var relative = Path.GetRelativePath(root, sourcePath).Replace('\\', '/');
                if (!string.Equals(relative, source.RelativePath, StringComparison.Ordinal))
                    throw Failure("migration.transaction.source.invalid", "A source fingerprint escaped the project.");
                var bytes = File.ReadAllBytes(sourcePath);
                if (bytes.LongLength != source.Length || !string.Equals(HashBytes(bytes), source.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw Failure("migration.transaction.preview.stale", $"Source '{source.RelativePath}' changed during migration.");
                var target = Path.Combine(path, source.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!IsWithin(path, target))
                    throw Failure("migration.transaction.backup.path.invalid", "A backup target escaped the backup directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, bytes);
                var copied = File.ReadAllBytes(target);
                if (copied.LongLength != source.Length || !string.Equals(HashBytes(copied), source.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw Failure("migration.transaction.backup.verify.failed", $"Backup verification failed for '{source.RelativePath}'.");
            }
            return path;
        }
        catch (CanonicalProjectMigrationException) { throw; }
        catch (Exception exception)
        {
            throw Failure("migration.transaction.backup.failed", "Could not create the complete migration backup.", exception);
        }
    }

    private void WriteCandidate(CanonicalProjectMigrationWriteCandidate candidate, string destination)
    {
        try
        {
            _writer.Write(destination, candidate.Contents, temporaryPath =>
            {
                var staged = File.ReadAllText(temporaryPath);
                if (candidate.IsMembership)
                {
                    var manifest = CanonicalStoryMembershipSerializer.Deserialize(staged);
                    if (!string.Equals(manifest.StoryId, candidate.ResourceId, StringComparison.Ordinal))
                        throw Failure("migration.transaction.staged.invalid", "Staged membership identity changed.");
                }
                else
                {
                    var envelope = GraphResourceEnvelopeSerializer.Deserialize(staged);
                    if (candidate.ResourceKind is null || envelope.ResourceKind != candidate.ResourceKind.Value
                        || !string.Equals(envelope.Id, candidate.ResourceId, StringComparison.Ordinal))
                        throw Failure("migration.transaction.staged.invalid", "Staged envelope identity changed.");
                }
                if (!string.Equals(HashBytes(File.ReadAllBytes(temporaryPath)), candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw Failure("migration.transaction.staged.invalid", "Staged candidate bytes changed.");
            });
            if (!File.Exists(destination)) throw Failure("migration.transaction.write.failed", $"Writer did not create '{destination}'.");
            var bytes = File.ReadAllBytes(destination);
            if (!string.Equals(HashBytes(bytes), candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                throw Failure("migration.transaction.write.verify.failed", $"Written bytes do not match '{candidate.RelativePath}'.");
        }
        catch (CanonicalProjectMigrationException) { throw; }
        catch (Exception exception)
        {
            throw Failure("migration.transaction.write.failed", $"Could not write '{candidate.RelativePath}'.", exception);
        }
    }

    private static void EnsureParent(string destination, string root, List<string> created)
    {
        var parent = Path.GetDirectoryName(destination)!;
        var missing = new Stack<string>();
        for (var current = parent; !Directory.Exists(current); current = Path.GetDirectoryName(current)!)
        {
            if (!IsWithin(root, current)) throw Failure("migration.transaction.destination.invalid", "Canonical destination escaped the project.");
            missing.Push(current);
        }
        while (missing.Count != 0)
        {
            var directory = missing.Pop();
            Directory.CreateDirectory(directory);
            created.Add(directory);
        }
    }

    private static void Rollback(IEnumerable<string> written, IEnumerable<string> createdDirectories)
    {
        var errors = new List<Exception>();
        foreach (var path in written.Reverse())
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception) { errors.Add(exception); }
        foreach (var path in createdDirectories.OrderByDescending(x => x.Length).ThenByDescending(x => x, StringComparer.Ordinal))
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: false); }
            catch (IOException exception) { errors.Add(exception); }
            catch (UnauthorizedAccessException exception) { errors.Add(exception); }
        if (errors.Count != 0) throw new AggregateException(errors);
    }

    private static void AppendLog(string root, bool backupExists, string line)
    {
        if (!backupExists) return;
        try { File.AppendAllText(Path.Combine(root, "migration.log"), line + Environment.NewLine); }
        catch (Exception) { /* migration outcome must never be masked by logging */ }
    }

    private static string FullPath(string path, string code)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { throw Failure(code, exception.Message, exception); }
    }

    private static bool IsWithin(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static CanonicalProjectMigrationException Failure(string code, string message, Exception? inner = null)
        => new(code, message, inner);
}
