using System.ComponentModel;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class NavigationViewModelTests
{
    [TestMethod]
    public void ItemsHaveStableOrderAndStoryIsSelectedByDefault()
    {
        var viewModel = new NavigationViewModel();

        CollectionAssert.AreEqual(
            new[] { "Story", "Settings" },
            viewModel.Items.Select(item => item.Page).ToArray());
        Assert.AreEqual("Story", viewModel.SelectedItem.Page);
        Assert.IsTrue(viewModel.Items.All(item => item.IsEnabled));
    }

    [TestMethod]
    public void SelectingItemRaisesPropertyChanged()
    {
        var viewModel = new NavigationViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedItem = viewModel.Items[1];

        Assert.AreEqual("Settings", viewModel.SelectedItem.Page);
        CollectionAssert.Contains(changedProperties, nameof(NavigationViewModel.SelectedItem));
    }
}
