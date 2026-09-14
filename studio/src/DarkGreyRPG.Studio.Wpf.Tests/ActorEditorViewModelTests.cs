using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ActorEditorViewModelTests
{
    [TestMethod]
    public void PortraitEditingIsTypedOnlyAndUndoPreservesTheOwnedReference()
    {
        var legacy = CreateEditor();
        Assert.IsFalse(legacy.SupportsPortraits);
        string image = "media/" + new string('a', 64) + ".png";
        legacy.DefaultPortraitRef = image;
        legacy.SetPortraitVariants([new("开心", image)]);
        Assert.IsNull(legacy.DefaultPortraitRef);
        Assert.AreEqual(0, legacy.PortraitVariants.Count);
        var typed = new ActorEditorViewModel(ActorDocument.FromResource(new IndividualActorResource { NpcId = "hero", DisplayName = "Hero" }, Path.Combine(Path.GetTempPath(), "hero.json")));
        Assert.IsTrue(typed.SupportsPortraits);
        typed.DefaultPortraitRef = image;
        typed.SetPortraitVariants([new("开心", image)]);
        Assert.IsTrue(typed.UndoCommand.CanExecute(null));
        typed.UndoCommand.Execute(null);
        Assert.AreEqual(image, typed.DefaultPortraitRef);
        Assert.AreEqual(0, typed.PortraitVariants.Count);
    }

    [TestMethod]
    public void UndoAndRedoRestoreDisplayNameNotesAndTags()
    {
        var viewModel = CreateEditor();

        viewModel.DisplayName = "法师";
        viewModel.Notes = "负责战斗任务";
        viewModel.TagsText = "combat, boss";

        Assert.IsTrue(viewModel.CanUndo);
        Assert.IsFalse(viewModel.CanRedo);
        Assert.IsTrue(viewModel.UndoCommand.CanExecute(null));

        viewModel.UndoCommand.Execute(null);
        Assert.AreEqual("法师", viewModel.DisplayName);
        Assert.AreEqual("负责战斗任务", viewModel.Notes);
        Assert.AreEqual("base", viewModel.TagsText);
        Assert.IsTrue(viewModel.Document.IsDirty);

        viewModel.UndoCommand.Execute(null);
        Assert.AreEqual("初始备注", viewModel.Notes);

        viewModel.UndoCommand.Execute(null);
        Assert.AreEqual("Teacher", viewModel.DisplayName);
        Assert.IsFalse(viewModel.Document.IsDirty);
        Assert.AreEqual("已保存", viewModel.SaveStateText);
        Assert.IsFalse(viewModel.CanUndo);

        viewModel.RedoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);

        Assert.AreEqual("法师", viewModel.DisplayName);
        Assert.AreEqual("负责战斗任务", viewModel.Notes);
        Assert.AreEqual("combat, boss", viewModel.TagsText);
        CollectionAssert.AreEqual(new[] { "combat", "boss" }, viewModel.Document.Tags.ToArray());
        Assert.IsFalse(viewModel.CanRedo);
    }

    [TestMethod]
    public void UndoRestoresValidationAndDirtyState()
    {
        var viewModel = CreateEditor();

        viewModel.DisplayName = string.Empty;

        Assert.IsTrue(viewModel.Document.IsDirty);
        Assert.IsFalse(viewModel.CanSave);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ValidationText));

        viewModel.UndoCommand.Execute(null);

        Assert.AreEqual("Teacher", viewModel.DisplayName);
        Assert.IsFalse(viewModel.Document.IsDirty);
        Assert.AreEqual(string.Empty, viewModel.ValidationText);
        Assert.IsFalse(viewModel.CanSave);
    }

    [TestMethod]
    public void NewEditAfterUndoClearsRedoHistory()
    {
        var viewModel = CreateEditor();

        viewModel.DisplayName = "法师";
        viewModel.Notes = "新备注";
        viewModel.UndoCommand.Execute(null);

        Assert.IsTrue(viewModel.CanRedo);
        Assert.AreEqual("初始备注", viewModel.Notes);

        viewModel.TagsText = "quest";

        Assert.IsFalse(viewModel.CanRedo);
        Assert.IsFalse(viewModel.RedoCommand.CanExecute(null));

        viewModel.UndoCommand.Execute(null);
        Assert.AreEqual("base", viewModel.TagsText);
        Assert.AreEqual("初始备注", viewModel.Notes);
        Assert.AreEqual("法师", viewModel.DisplayName);
    }

    private static ActorEditorViewModel CreateEditor()
    {
        var document = ActorDocument.FromResource(
            new ActorResource
            {
                Id = "teacher",
                DisplayName = "Teacher",
                Notes = "初始备注",
                Tags = ["base"],
            },
            Path.Combine(Path.GetTempPath(), "darkgrey-rpg-actor.json"));

        return new ActorEditorViewModel(document);
    }
}
