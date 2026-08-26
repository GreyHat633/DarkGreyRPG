using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class OutputViewModelTests
{
    [TestMethod]
    public void AppendStoresTimestampKindAndSourceAndNotifiesCollection()
    {
        var now = new DateTimeOffset(2026, 8, 18, 12, 30, 0, TimeSpan.Zero);
        var viewModel = new OutputViewModel(now: () => now);
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.Append("Could not save actor.", OutputKind.Error, "ActorRepository");

        var entry = viewModel.Entries.Single();
        Assert.AreEqual(now, entry.Timestamp);
        Assert.AreEqual("Could not save actor.", entry.Message);
        Assert.AreEqual(OutputKind.Error, entry.Kind);
        Assert.AreEqual("ActorRepository", entry.Source);
        Assert.AreEqual(1, viewModel.Count);
        CollectionAssert.Contains(changes, nameof(OutputViewModel.Count));
    }

    [TestMethod]
    public void AppendBoundsHistoryByRemovingOldestEntriesAndClearNotifies()
    {
        var viewModel = new OutputViewModel(2, () => DateTimeOffset.UnixEpoch);
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.Append("first");
        viewModel.Append("second");
        viewModel.Append("third");

        CollectionAssert.AreEqual(new[] { "second", "third" }, viewModel.Entries.Select(entry => entry.Message).ToArray());
        Assert.AreEqual(2, viewModel.Count);

        viewModel.Clear();

        Assert.AreEqual(0, viewModel.Count);
        CollectionAssert.Contains(changes, nameof(OutputViewModel.Count));
    }
}
