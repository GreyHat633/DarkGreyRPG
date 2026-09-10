using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalIdentityNamespaceDialogViewModelTests
{
    [TestMethod]
    public void CanonicalActorPreservesExactFullIdAndAcceptsLegacyBareId()
    {
        const string fullId = "Author_NS:town.guard-1";
        var viewModel = new CanonicalActorIdentityDialogViewModel(
            CanonicalStoryActorKind.Individual,
            fullId);

        Assert.IsTrue(DgrResourceId.IsFullId(fullId));
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(fullId, viewModel.Id);
        Assert.AreEqual(fullId, viewModel.NormalizedSuggestion);
        Assert.IsFalse(viewModel.HasSuggestion);

        viewModel.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual(fullId, viewModel.Id);

        var bare = new CanonicalActorIdentityDialogViewModel(
            CanonicalStoryActorKind.Individual,
            "  Town Guard!  ");
        Assert.IsFalse(bare.CanConfirm);
        Assert.AreEqual("town_guard", bare.NormalizedSuggestion);
        bare.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual("town_guard", bare.Id);
        Assert.IsTrue(bare.CanConfirm);
    }

    [TestMethod]
    public void CanonicalItemPreservesExactFullIdAndRejectsMalformedOrWhitespaceIds()
    {
        const string fullId = "ModAuthor:iron.sword-1";
        var valid = CanonicalItemIdentityDialogViewModel.ForCreate(
            CanonicalStoryItemKind.Individual,
            fullId);

        Assert.IsTrue(valid.CanConfirm);
        Assert.AreEqual(fullId, valid.NormalizedSuggestion);
        valid.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual(fullId, valid.Id);

        foreach (var invalid in new[] { " ModAuthor:iron.sword-1", "ModAuthor:iron.sword-1 ", "ModAuthor:Iron/Sword" })
        {
            var viewModel = CanonicalItemIdentityDialogViewModel.ForCreate(
                CanonicalStoryItemKind.Individual,
                invalid);
            Assert.IsFalse(viewModel.CanConfirm, invalid);
            Assert.AreEqual(invalid, viewModel.NormalizedSuggestion);
        }
    }

    [TestMethod]
    public void CanonicalGraphAndLegacyResourceDialogsPreserveFullIds()
    {
        const string fullId = "StudioTeam:quest.main-1";
        var graph = CanonicalResourceIdentityDialogViewModel.ForCreate(
            GraphResourceKind.Task,
            fullId);
        var legacy = ResourceIdentityDialogViewModel.ForCreate(
            ProjectResourceType.Quest,
            fullId);

        Assert.IsTrue(graph.CanConfirm);
        Assert.IsTrue(legacy.CanConfirm);
        Assert.AreEqual(fullId, graph.NormalizedSuggestion);
        Assert.AreEqual(fullId, legacy.NormalizedSuggestion);

        graph.ApplySuggestionCommand.Execute(null);
        legacy.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual(fullId, graph.Id);
        Assert.AreEqual(fullId, legacy.Id);

        var malformed = CanonicalResourceIdentityDialogViewModel.ForCreate(
            GraphResourceKind.Session,
            " StudioTeam:session");
        Assert.IsFalse(malformed.CanConfirm);
        Assert.AreEqual(" StudioTeam:session", malformed.NormalizedSuggestion);
    }
    [TestMethod]
    public void UppercaseNpcItemAndGroupIdsRemainExactWithoutLowercaseSuggestions()
    {
        foreach (var id in new[] { "Team:Guard", "Team:guard", "Team:GUARD" })
        {
            var npc = new CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind.Individual, id);
            var group = new CanonicalActorIdentityDialogViewModel(CanonicalStoryActorKind.Collective, id);
            var item = CanonicalItemIdentityDialogViewModel.ForCreate(CanonicalStoryItemKind.Individual, id);
            Assert.IsTrue(npc.CanConfirm);
            Assert.IsTrue(group.CanConfirm);
            Assert.IsTrue(item.CanConfirm);
            Assert.IsFalse(npc.HasSuggestion);
            Assert.IsFalse(group.HasSuggestion);
            Assert.IsFalse(item.HasSuggestion);
            Assert.AreEqual(id, npc.Id);
            Assert.AreEqual(id, group.Id);
            Assert.AreEqual(id, item.Id);
        }
    }
}
