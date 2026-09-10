using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ResourceNamespacePrefixTests
{
    [TestMethod]
    public void CanonicalActorCreateLocksNamespaceForIndividualAndCollectiveIds()
    {
        foreach (var kind in new[] { CanonicalStoryActorKind.Individual, CanonicalStoryActorKind.Collective })
        {
            var viewModel = new CanonicalActorIdentityDialogViewModel(kind, "Team:Guard");

            AssertActorPrefix(viewModel, "Guard");
            viewModel.EditableId = "UpperCase";

            Assert.AreEqual("Team:UpperCase", viewModel.Id);
            Assert.AreEqual("UpperCase", viewModel.EditableId);
            Assert.IsTrue(viewModel.CanConfirm);

            viewModel.EditableId = "Other:Id";
            Assert.AreEqual("Team:Other:Id", viewModel.Id);
            Assert.IsFalse(viewModel.CanConfirm);
            Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        }
    }

    [TestMethod]
    public void CanonicalItemCreateLocksNamespaceForIndividualAndCollectiveIds()
    {
        foreach (var kind in new[] { CanonicalStoryItemKind.Individual, CanonicalStoryItemKind.Collective })
        {
            var viewModel = CanonicalItemIdentityDialogViewModel.ForCreate(kind, "Team:Token");

            AssertItemPrefix(viewModel, "Token");
            viewModel.EditableId = "UpperCase";

            Assert.AreEqual("Team:UpperCase", viewModel.Id);
            Assert.AreEqual("UpperCase", viewModel.EditableId);
            Assert.IsTrue(viewModel.CanConfirm);

            viewModel.EditableId = "Other:Id";
            Assert.AreEqual("Team:Other:Id", viewModel.Id);
            Assert.IsFalse(viewModel.CanConfirm);
        }
    }

    [TestMethod]
    public void CanonicalResourceCreateLocksNamespaceForSessionAndTaskIds()
    {
        foreach (var kind in new[] { GraphResourceKind.Session, GraphResourceKind.Task })
        {
            var viewModel = CanonicalResourceIdentityDialogViewModel.ForCreate(kind, "Team:Flow");

            AssertCanonicalResourcePrefix(viewModel, "Flow");
            viewModel.EditableId = "UpperCase";

            Assert.AreEqual("Team:UpperCase", viewModel.Id);
            Assert.AreEqual("UpperCase", viewModel.EditableId);
            Assert.IsTrue(viewModel.CanConfirm);

            viewModel.EditableId = "Other:Id";
            Assert.AreEqual("Team:Other:Id", viewModel.Id);
            Assert.IsFalse(viewModel.CanConfirm);
        }
    }

    [TestMethod]
    public void LegacyActorCreateAndCopyPreserveNamespacePrefix()
    {
        var create = ActorIdentityDialogViewModel.ForCreate("Team:Guard");
        var copy = ActorIdentityDialogViewModel.ForImport("Guard", "Team:GuardCopy");

        AssertActorPrefix(create, "Guard");
        AssertActorPrefix(copy, "GuardCopy");

        create.EditableId = "UpperCase";
        copy.EditableId = "UpperCase";
        Assert.AreEqual("Team:UpperCase", create.Id);
        Assert.AreEqual("Team:UpperCase", copy.Id);

        create.EditableId = "Other:Id";
        copy.EditableId = "Other:Id";
        Assert.IsFalse(create.CanConfirm);
        Assert.IsFalse(copy.CanConfirm);
    }

    [TestMethod]
    public void LegacyResourceCreateAndCopyPreserveNamespaceForEveryResourceType()
    {
        foreach (var type in new[] { ProjectResourceType.Dialogue, ProjectResourceType.Quest, ProjectResourceType.Story })
        {
            var create = ResourceIdentityDialogViewModel.ForCreate(type, "Team:Resource");
            var copy = ResourceIdentityDialogViewModel.ForImport(type, "Resource", "Team:ResourceCopy");

            AssertResourcePrefix(create, "Resource");
            AssertResourcePrefix(copy, "ResourceCopy");

            create.EditableId = "UpperCase";
            copy.EditableId = "UpperCase";
            Assert.AreEqual("Team:UpperCase", create.Id);
            Assert.AreEqual("Team:UpperCase", copy.Id);

            create.EditableId = "Other:Id";
            copy.EditableId = "Other:Id";
            Assert.IsFalse(create.CanConfirm);
            Assert.IsFalse(copy.CanConfirm);
        }
    }

    [TestMethod]
    public void BareLegacySuggestionsStillApplyAsLocalIds()
    {
        var actors = new[]
        {
            new CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind.Individual, "  Bare Actor!  "),
            new CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind.Collective, "  Bare Group!  "),
        };
        foreach (var canonicalActor in actors)
        {
            Assert.IsFalse(canonicalActor.CanConfirm);
            canonicalActor.ApplySuggestionCommand.Execute(null);
            Assert.IsTrue(canonicalActor.CanConfirm);
            Assert.IsFalse(canonicalActor.Id.Contains(':'));
        }

        var items = new[]
        {
            CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Individual, "  Bare Item!  "),
            CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Collective, "  Bare Items!  "),
        };
        foreach (var item in items)
        {
            Assert.IsFalse(item.CanConfirm);
            item.ApplySuggestionCommand.Execute(null);
            Assert.IsTrue(item.CanConfirm);
            Assert.IsFalse(item.Id.Contains(':'));
        }

        foreach (var kind in new[] { GraphResourceKind.Session, GraphResourceKind.Task })
        {
            var resource = CanonicalResourceIdentityDialogViewModel.ForCreate(kind, "  Bare Flow!  ");
            Assert.IsFalse(resource.CanConfirm);
            resource.ApplySuggestionCommand.Execute(null);
            Assert.IsTrue(resource.CanConfirm);
            Assert.IsFalse(resource.Id.Contains(':'));
        }

        var actor = ActorIdentityDialogViewModel.ForCreate("  Bare Actor!  ");
        actor.ApplySuggestionCommand.Execute(null);
        Assert.IsTrue(actor.CanConfirm);
        Assert.IsFalse(actor.Id.Contains(':'));

        var legacyResource = ResourceIdentityDialogViewModel.ForCreate(ProjectResourceType.Story, "  Bare Story!  ");
        legacyResource.ApplySuggestionCommand.Execute(null);
        Assert.IsTrue(legacyResource.CanConfirm);
        Assert.IsFalse(legacyResource.Id.Contains(':'));
    }

    private static void AssertActorPrefix(CanonicalActorIdentityDialogViewModel viewModel, string localId)
    {
        Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        Assert.AreEqual("Team : ", viewModel.NamespacePrefixDisplay);
        Assert.IsTrue(viewModel.HasLockedNamespace);
        Assert.AreEqual(localId, viewModel.EditableId);
        Assert.AreEqual($"Team:{localId}", viewModel.Id);
        Assert.AreEqual($"Team:{localId}", viewModel.NormalizedSuggestion);
    }

    private static void AssertActorPrefix(ActorIdentityDialogViewModel viewModel, string localId)
    {
        Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        Assert.AreEqual("Team : ", viewModel.NamespacePrefixDisplay);
        Assert.IsTrue(viewModel.HasLockedNamespace);
        Assert.AreEqual(localId, viewModel.EditableId);
        Assert.AreEqual($"Team:{localId}", viewModel.Id);
        Assert.AreEqual($"Team:{localId}", viewModel.NormalizedSuggestion);
    }

    private static void AssertItemPrefix(CanonicalItemIdentityDialogViewModel viewModel, string localId)
    {
        Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        Assert.AreEqual("Team : ", viewModel.NamespacePrefixDisplay);
        Assert.IsTrue(viewModel.HasLockedNamespace);
        Assert.AreEqual(localId, viewModel.EditableId);
        Assert.AreEqual($"Team:{localId}", viewModel.Id);
        Assert.AreEqual($"Team:{localId}", viewModel.NormalizedSuggestion);
    }

    private static void AssertCanonicalResourcePrefix(CanonicalResourceIdentityDialogViewModel viewModel, string localId)
    {
        Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        Assert.AreEqual("Team : ", viewModel.NamespacePrefixDisplay);
        Assert.IsTrue(viewModel.HasLockedNamespace);
        Assert.AreEqual(localId, viewModel.EditableId);
        Assert.AreEqual($"Team:{localId}", viewModel.Id);
        Assert.AreEqual($"Team:{localId}", viewModel.NormalizedSuggestion);
    }

    private static void AssertResourcePrefix(ResourceIdentityDialogViewModel viewModel, string localId)
    {
        Assert.AreEqual("Team:", viewModel.NamespacePrefix);
        Assert.AreEqual("Team : ", viewModel.NamespacePrefixDisplay);
        Assert.IsTrue(viewModel.HasLockedNamespace);
        Assert.AreEqual(localId, viewModel.EditableId);
        Assert.AreEqual($"Team:{localId}", viewModel.Id);
        Assert.AreEqual($"Team:{localId}", viewModel.NormalizedSuggestion);
    }
}
