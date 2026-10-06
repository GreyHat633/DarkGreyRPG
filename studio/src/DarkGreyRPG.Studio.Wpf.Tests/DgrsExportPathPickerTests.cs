using System.IO;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class DgrsExportPathPickerTests
{
    [TestMethod]
    [DataRow("测试故事", "测试故事.dgrs")]
    [DataRow("Opening", "Opening.dgrs")]
    [DataRow(" 第一章：旅人/酒馆? ", "第一章：旅人_酒馆_.dgrs")]
    [DataRow("CON", "_CON.dgrs")]
    [DataRow("nul.旅人", "_nul.旅人.dgrs")]
    [DataRow("LPT1", "_LPT1.dgrs")]
    [DataRow("故事. ", "故事.dgrs")]
    [DataRow("...", "故事.dgrs")]
    public void DisplayFileName_UsesDisplayNameAndProducesSafeWindowsLeaf(string displayName, string expected)
    {
        Assert.AreEqual(expected, DgrsExportPathPicker.DisplayFileName(displayName));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void DisplayFileName_RejectsEmptyName(string displayName)
        => Assert.ThrowsExactly<ArgumentException>(() => DgrsExportPathPicker.DisplayFileName(displayName));

    [TestMethod]
    public void GroupFileName_UsesGroupDisplayNameAndCompleteExtension()
        => Assert.AreEqual("故事组（1）.dgrs.g", DgrsExportPathPicker.DisplayFileName("故事组（1）", group: true));

    [TestMethod]
    public void LongName_ReservesExtensionAndDoesNotSplitSurrogatePair()
    {
        var result = DgrsExportPathPicker.DisplayFileName(new string('中', 239) + "😀" + new string('文', 50), group: true);
        Assert.AreEqual(new string('中', 239) + ".dgrs.g", result);
        Assert.IsTrue(result.Length < 256);
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

    [TestMethod]
    public void ResolveInitialDirectory_FollowsRuntimeAndRepositoryRename()
    {
        using var directories = new TemporaryDirectories();
        var oldPath = Path.Combine(directories.Remembered, "DarkGrey_RPG", "run", "client", "darkgrey_rpg_story_packages");
        var newPath = Path.Combine(directories.Remembered, "DarkGreyRPG", "run", "client", "DarkGreyRPG", "StoryPackages");
        Directory.CreateDirectory(newPath);
        Assert.AreEqual(newPath, DgrsExportPathPicker.ResolveInitialDirectory(oldPath, directories.Fallback));
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
