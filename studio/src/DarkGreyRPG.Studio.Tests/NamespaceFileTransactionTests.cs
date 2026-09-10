using System.Text;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class NamespaceFileTransactionTests
{
    [TestMethod]
    public void SuccessfulReplaceCreateDeletePreservesUnrelatedFiles()
    {
        using var project = new TestProjectDirectory();
        var replace = Path.Combine(project.Root, "actors", "one.json");
        var deleted = Path.Combine(project.Root, "actors", "remove.json");
        var unrelated = Path.Combine(project.Root, "keep.txt");
        File.WriteAllText(replace, "old");
        File.WriteAllText(deleted, "remove");
        File.WriteAllText(unrelated, "keep");

        new NamespaceFileTransaction().Apply(project.Root,
            [
                new("actors/one.json", Bytes("old"), Bytes("new")),
                new("actors/two.json", null, Bytes("created")),
                new("actors/remove.json", Bytes("remove"), null),
            ],
            () => { });

        Assert.AreEqual("new", File.ReadAllText(replace));
        Assert.AreEqual("created", File.ReadAllText(Path.Combine(project.Root, "actors", "two.json")));
        Assert.IsFalse(File.Exists(deleted));
        Assert.AreEqual("keep", File.ReadAllText(unrelated));
        Assert.AreEqual(0, Directory.GetFiles(project.Root, ".dgr-namespace-transaction-*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public void SecondWriteFailureRestoresFirstWriteAndLeavesCreatedFileAbsent()
    {
        using var project = new TestProjectDirectory();
        var first = Path.Combine(project.Root, "first.txt");
        File.WriteAllText(first, "before");
        var calls = 0;

        Assert.ThrowsExactly<IOException>(() => new NamespaceFileTransaction((path, bytes) =>
        {
            if (++calls == 2) throw new IOException("second write failure");
            File.WriteAllBytes(path, bytes);
        }).Apply(project.Root,
            [
                new("first.txt", Bytes("before"), Bytes("after")),
                new("nested/second.txt", null, Bytes("created")),
            ],
            () => { }));

        Assert.AreEqual("before", File.ReadAllText(first));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "nested", "second.txt")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "nested")));
    }

    [TestMethod]
    public void DeletionFailureRestoresEarlierDeletionAndWrites()
    {
        using var project = new TestProjectDirectory();
        var first = Path.Combine(project.Root, "first.txt");
        var second = Path.Combine(project.Root, "second.txt");
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");
        var calls = 0;

        Assert.ThrowsExactly<IOException>(() => new NamespaceFileTransaction(
            deleteFile: path =>
            {
                if (++calls == 2) throw new IOException("delete failure");
                File.Delete(path);
            }).Apply(project.Root,
                [new("first.txt", Bytes("one"), null), new("second.txt", Bytes("two"), null)],
                () => { }));

        Assert.AreEqual("one", File.ReadAllText(first));
        Assert.AreEqual("two", File.ReadAllText(second));
    }

    [TestMethod]
    public void StaleSourceIsRejectedBeforeValidatorAndWrites()
    {
        using var project = new TestProjectDirectory();
        var path = Path.Combine(project.Root, "source.txt");
        File.WriteAllText(path, "actual");
        var validated = false;

        Assert.ThrowsExactly<InvalidOperationException>(() => new NamespaceFileTransaction().Apply(
            project.Root,
            [new("source.txt", Bytes("stale"), Bytes("new"))],
            () => validated = true));

        Assert.IsFalse(validated);
        Assert.AreEqual("actual", File.ReadAllText(path));
    }

    [TestMethod]
    public void NewlyAppearedDestinationIsRejectedByPostValidation()
    {
        using var project = new TestProjectDirectory();
        var path = Path.Combine(project.Root, "destination.txt");

        Assert.ThrowsExactly<InvalidOperationException>(() => new NamespaceFileTransaction().Apply(
            project.Root,
            [new("destination.txt", null, Bytes("new"))],
            () => File.WriteAllText(path, "appeared")));

        Assert.AreEqual("appeared", File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(project.Root, ".dgr-namespace-transaction-*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public void ValidatorFailureDoesNotMutateFiles()
    {
        using var project = new TestProjectDirectory();
        var path = Path.Combine(project.Root, "source.txt");
        File.WriteAllText(path, "before");

        Assert.ThrowsExactly<InvalidDataException>(() => new NamespaceFileTransaction().Apply(
            project.Root,
            [new("source.txt", Bytes("before"), Bytes("after"))],
            () => throw new InvalidDataException("invalid final project")));

        Assert.AreEqual("before", File.ReadAllText(path));
    }

    [TestMethod]
    public void InvalidAndAliasedPathsAreRejected()
    {
        using var project = new TestProjectDirectory();
        var invalid = new[] { "C:/outside.txt", "../outside.txt", "folder/../../outside.txt", "folder:stream.txt", "folder./file.json", "folder /file.json", "CON.json", "dir/LPT1.txt" };
        foreach (var path in invalid)
        {
            Assert.ThrowsExactly<ArgumentException>(() => new NamespaceFileTransaction().Apply(
                project.Root, [new(path, null, Bytes("x"))], () => { }));
        }

        Assert.ThrowsExactly<ArgumentException>(() => new NamespaceFileTransaction().Apply(
            project.Root,
            [new("folder/file.txt", null, Bytes("x")), new("folder\\FILE.txt", null, Bytes("y"))],
            () => { }));
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
