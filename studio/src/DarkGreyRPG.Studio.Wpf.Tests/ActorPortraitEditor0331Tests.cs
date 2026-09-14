using System.Windows.Automation;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ActorPortraitEditor0331Tests
{
    [TestMethod]
    public void InlineControlHasNoDialogAndExposesPortraitActions()
    {
        RunOnSta(() =>
        {
            var control = new ActorPortraitEditor();
            Assert.IsInstanceOfType(control, typeof(System.Windows.Controls.UserControl));
            Assert.IsNull(control.Editor);
            Assert.IsNotNull(control.Content);
        });
    }

    [TestMethod]
    public void ReferencedVariantRenameAndRemoveAreRejectedWithoutUndoMutation()
    {
        var image = "media/" + new string('a', 64) + ".png";
        var document = ActorDocument.FromResource(
            new IndividualActorResource { NpcId = "hero", DisplayName = "Hero", PortraitVariants = [new("angry", image)] },
            Path.Combine(Path.GetTempPath(), "hero.json"));
        var editor = new ActorEditorViewModel(document)
        {
            IsPortraitVariantReferenced = _ => true,
        };
        var variant = editor.PortraitVariants[0];

        Assert.IsFalse(editor.TryRenamePortraitVariant(variant, "calm"));
        Assert.AreEqual("angry", editor.PortraitVariants[0].Name);
        Assert.IsFalse(editor.TryRemovePortraitVariant(variant));
        Assert.HasCount(1, editor.PortraitVariants);
        Assert.IsFalse(editor.CanUndo);
    }

    [TestMethod]
    public void ReadOnlyEditorCanDisplayButCannotClearPortrait()
    {
        var editor = new ActorEditorViewModel(ActorDocument.FromResource(
            new IndividualActorResource { NpcId = "hero", DisplayName = "Hero", DefaultPortraitRef = "media/" + new string('b', 64) + ".png" },
            Path.Combine(Path.GetTempPath(), "hero-readonly.json"))) { IsReadOnly = true };
        RunOnSta(() =>
        {
            var control = new ActorPortraitEditor { IsReadOnly = true };
            control.SetEditor(editor);
            Assert.IsFalse(control.ClearDefault());
            Assert.IsNotNull(editor.DefaultPortraitRef);
            Assert.IsTrue(editor.IsReadOnly);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) throw new AssertFailedException(failure.ToString());
    }
}
