using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CurrentIdentityAuthoring0336Tests
{
    private static readonly StoryUid Owner = StoryUid.Parse("ST-2345-6789-ABCD-EFGH");
    private static string Key(ResourceKind kind) => new ResourceAddress(Owner, kind, "hidden").ToKey();

    [TestMethod]
    public void ResourceDialogsPreserveAllocatedAddressAndValidateKind()
    {
        var actor = new CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind.Individual, Key(ResourceKind.Actor));
        var item = CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Individual, Key(ResourceKind.Item));
        var group = CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Collective, Key(ResourceKind.ItemGroup));
        var session = CanonicalResourceIdentityDialogViewModel.ForCreate(GraphResourceKind.Session, Key(ResourceKind.Session));
        var task = CanonicalResourceIdentityDialogViewModel.ForCreate(GraphResourceKind.Task, Key(ResourceKind.Task));
        actor.Id = "Author:renamed"; item.Id = "Author:renamed"; group.Id = "Author:renamed";
        session.Id = "Author:renamed"; task.Id = "Author:renamed";
        Assert.AreEqual(Key(ResourceKind.Actor), actor.Id);
        Assert.AreEqual(Key(ResourceKind.Item), item.Id);
        Assert.AreEqual(Key(ResourceKind.ItemGroup), group.Id);
        Assert.AreEqual(Key(ResourceKind.Session), session.Id);
        Assert.AreEqual(Key(ResourceKind.Task), task.Id);
        Assert.IsFalse(actor.CanConfirm || item.CanConfirm || group.CanConfirm || session.CanConfirm || task.CanConfirm);
        actor.DisplayName = "老板"; item.DisplayName = "铜币"; group.DisplayName = "药剂";
        session.DisplayName = "初见"; task.DisplayName = "委托";
        Assert.IsTrue(actor.CanConfirm && item.CanConfirm && group.CanConfirm && session.CanConfirm && task.CanConfirm);
        Assert.IsFalse(CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Individual, Key(ResourceKind.ItemGroup)).CanConfirm);
        Assert.IsFalse(CanonicalResourceIdentityDialogViewModel.ForCreate(GraphResourceKind.Task, Key(ResourceKind.Session)).CanConfirm);
    }

    [TestMethod]
    public void MetadataEditCannotChangeIdentityAndPresentationHidesLocalKey()
    {
        var key = Key(ResourceKind.Actor);
        var dialog = new DisplayNameDialogViewModel("角色", key, "原名称", allowIdentityEdit: true);
        dialog.LocalId = "other";
        dialog.DisplayName = "新名称";
        Assert.AreEqual(key, dialog.NewId);
        Assert.IsFalse(dialog.AllowIdentityEdit);
        Assert.IsTrue(dialog.CanConfirm);
        Assert.AreEqual(string.Empty, dialog.IdentityText);
        Assert.IsFalse(ResourceIdentityPresentation.Format("NPC_ID", key).Contains("hidden", StringComparison.Ordinal));
        Assert.IsTrue(new DisplayNameDialogViewModel("故事", Owner.Value, "故事").IdentityText.Contains(Owner.Value, StringComparison.Ordinal));
    }
}
