using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ToastViewModelTests
{
    [TestMethod]
    public void ShowSetsMessageKindAndVisibilityWithNotifications()
    {
        var viewModel = new ToastViewModel();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.Show("Saved tavern owner.", ToastKind.Success);

        Assert.AreEqual("Saved tavern owner.", viewModel.Message);
        Assert.AreEqual(ToastKind.Success, viewModel.Kind);
        Assert.IsTrue(viewModel.IsVisible);
        CollectionAssert.Contains(changes, nameof(ToastViewModel.Message));
        CollectionAssert.Contains(changes, nameof(ToastViewModel.Kind));
        CollectionAssert.Contains(changes, nameof(ToastViewModel.IsVisible));
    }

    [TestMethod]
    public void DismissHidesToastAndRetainsLastMessageForTransitions()
    {
        var viewModel = new ToastViewModel();
        viewModel.Show("Unable to save: permission denied.", ToastKind.Error);

        viewModel.Dismiss();

        Assert.IsFalse(viewModel.IsVisible);
        Assert.AreEqual("Unable to save: permission denied.", viewModel.Message);
        Assert.AreEqual(ToastKind.Error, viewModel.Kind);
    }
}
