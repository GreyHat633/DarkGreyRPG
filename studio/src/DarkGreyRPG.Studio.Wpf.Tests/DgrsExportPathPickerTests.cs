using System.IO;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class DgrsExportPathPickerTests
{
    [TestMethod]
    [DataRow("GreyHat_:Firest", "GreyHat_.Firest.dgrs")]
    [DataRow("MyPack:chapter_01", "MyPack.chapter_01.dgrs")]
    [DataRow("legacy_story", "legacy_story.dgrs")]
    public void DisplayFileName_UsesHumanReadableDotForNamespace(string storyId, string expected)
    {
        Assert.AreEqual(expected, DgrsExportPathPicker.DisplayFileName(storyId));
    }

    [TestMethod]
    public void ResolveInitialDirectory_UsesRememberedDirectoryWhenItExists()
    {
        using var directories = new TemporaryDirectories();

        var resolved = DgrsExportPathPicker.ResolveInitialDirectory(directories.Remembered, directories.Fallback);

        Assert.AreEqual(Path.GetFullPath(directories.Remembered), resolved);
    }

    [TestMethod]
    public void ResolveInitialDirectory_FallsBackWhenRememberedDirectoryWasDeleted()
    {
        using var directories = new TemporaryDirectories();
        Directory.Delete(directories.Remembered, recursive: true);

        var resolved = DgrsExportPathPicker.ResolveInitialDirectory(directories.Remembered, directories.Fallback);

        Assert.AreEqual(Path.GetFullPath(directories.Fallback), resolved);
    }

    private sealed class TemporaryDirectories : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DarkGreyRPG.FileDialogTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectories()
        {
            Remembered = Path.Combine(_root, "remembered");
            Fallback = Path.Combine(_root, "fallback");
            Directory.CreateDirectory(Remembered);
            Directory.CreateDirectory(Fallback);
        }

        public string Remembered { get; }
        public string Fallback { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
