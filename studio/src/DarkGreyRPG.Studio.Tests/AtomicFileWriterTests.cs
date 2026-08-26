using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class AtomicFileWriterTests
{
    [TestMethod]
    public void AtomicWriteReplacesExistingFileAndRemovesTemporaryFile()
    {
        using var project = new TestProjectDirectory();
        var path = Path.Combine(project.Root, "atomic.txt");
        File.WriteAllText(path, "old");

        new AtomicFileWriter().Write(path, "new");

        Assert.AreEqual("new", File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(project.Root, "*.tmp", SearchOption.TopDirectoryOnly).Length);
    }

    [TestMethod]
    public void FailedStagedValidationPreservesLastValidFile()
    {
        using var project = new TestProjectDirectory();
        var path = Path.Combine(project.Root, "atomic.txt");
        File.WriteAllText(path, "last-valid");

        Assert.ThrowsExactly<InvalidDataException>(() =>
            new AtomicFileWriter().Write(path, "invalid", _ => throw new InvalidDataException("Injected failure")));

        Assert.AreEqual("last-valid", File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(project.Root, "*.tmp", SearchOption.TopDirectoryOnly).Length);
    }
}
