using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class BottomPanelViewModelTests
{
    [TestMethod]
    public void TabsHaveStableOrderAndOutputIsSelectedByDefault()
    {
        var viewModel = new BottomPanelViewModel();

        CollectionAssert.AreEqual(
            new[] { "Output", "Problems", "Debugger", "Minecraft" },
            viewModel.Tabs.Select(tab => tab.Page).ToArray());
        Assert.AreEqual("Output", viewModel.SelectedTab.Page);
        Assert.IsFalse(viewModel.IsExpanded);
        Assert.IsGreaterThanOrEqualTo(BottomPanelViewModel.MinimumExpandedHeight, viewModel.ExpandedHeight);
        Assert.AreEqual(BottomPanelViewModel.HeaderHeight, viewModel.DockHeight);
    }

    [TestMethod]
    public void SelectingTabAndChangingExpansionStateAreObservable()
    {
        var viewModel = new BottomPanelViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedTab = viewModel.Tabs[1];
        viewModel.IsExpanded = false;
        viewModel.ExpandedHeight = 360;

        Assert.AreEqual("Problems", viewModel.SelectedTab.Page);
        Assert.IsFalse(viewModel.IsExpanded);
        Assert.AreEqual(360, viewModel.ExpandedHeight);
        Assert.AreEqual(BottomPanelViewModel.HeaderHeight, viewModel.DockHeight);
        CollectionAssert.Contains(changedProperties, nameof(BottomPanelViewModel.SelectedTab));
        CollectionAssert.Contains(changedProperties, nameof(BottomPanelViewModel.ExpandedHeight));
    }

    [TestMethod]
    public void SelectingAnotherTabExpandsAndSelectingCurrentTabToggles()
    {
        var viewModel = new BottomPanelViewModel();

        viewModel.SelectTab(viewModel.Tabs[1]);
        Assert.IsTrue(viewModel.IsExpanded);
        Assert.AreEqual("Problems", viewModel.SelectedTab.Page);

        viewModel.SelectTab(viewModel.Tabs[1]);
        Assert.IsFalse(viewModel.IsExpanded);
        Assert.AreEqual(BottomPanelViewModel.HeaderHeight, viewModel.DockHeight);
    }
}
