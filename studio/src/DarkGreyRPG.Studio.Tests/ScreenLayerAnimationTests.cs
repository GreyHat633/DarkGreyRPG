using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;
[TestClass]
public sealed class ScreenLayerAnimationTests
{
    [TestMethod]
    public void IndependentSequencesReplaySameImageAndStayHiddenAfterExit()
    {
        var fade = new ScreenLayerEffect("fade", "left", 2);
        var stable = new ScreenTransitionPreview.Sprite("stable", "stable", 0, 0, .2, .2, 1, 0, Enter: fade, Exit: fade);
        var added = stable with { Media = "new", Key = "new", X = .5, Enter = fade with { Duration = 1 } };
        var removed = stable with { Media = "old", Key = "old", X = .25 };
        var sample = ScreenLayerAnimation.Sample([stable, removed], [stable, added], ScreenLayerEffect.None, .5);
        Assert.AreEqual(.15625, sample.Single(s => s.Key == "stable").Alpha, .00001);
        Assert.AreEqual(.5, sample.Single(s => s.Key == "new").Alpha, .00001);
        Assert.IsFalse(sample.Any(s => s.Key == "old"));
        var done = ScreenLayerAnimation.Sample([stable, removed], [stable, added], ScreenLayerEffect.None, 5);
        Assert.IsEmpty(done);
    }

    [TestMethod]
    public void OrderedStepsDelayExitThenEnterAndMorphHaveSeparateClocks()
    {
        var picture = new ScreenTransitionPreview.Sprite("picture", "key", .8, 0, .2, .2, 1, 0,
            Animations: [new("enter", "fade", "left", 1, 1), new("exit", "fade", "left", 1, 2)]);
        Assert.IsEmpty(ScreenLayerAnimation.Sample([], [picture], ScreenLayerEffect.None, .5));
        Assert.AreEqual(.5, ScreenLayerAnimation.Sample([], [picture], ScreenLayerEffect.None, 1.5).Single().Alpha, .00001);
        Assert.AreEqual(1, ScreenLayerAnimation.Sample([], [picture], ScreenLayerEffect.None, 3).Single().Alpha);
        Assert.AreEqual(.5, ScreenLayerAnimation.Sample([], [picture], ScreenLayerEffect.None, 4.5).Single().Alpha, .00001);
        Assert.IsEmpty(ScreenLayerAnimation.Sample([], [picture], ScreenLayerEffect.None, 100));
        var reverse = picture with { Animations = [new("exit", "fade", "left", 1, 0), new("enter", "slide", "right", 1, 1)] };
        Assert.AreEqual(.5, ScreenLayerAnimation.Sample([], [reverse], ScreenLayerEffect.None, .5).Single().Alpha, .00001);
        Assert.IsEmpty(ScreenLayerAnimation.Sample([], [reverse], ScreenLayerEffect.None, 1.5));
        Assert.AreEqual(1, ScreenLayerAnimation.Sample([], [reverse], ScreenLayerEffect.None, 3).Single().Alpha);
        var motion = picture with { MorphDuration = 2, Animations = [new("exit", "fade", "left", 1, 0)] };
        var source = picture with { X = 0 };
        Assert.AreEqual(.4, ScreenLayerAnimation.Sample([source], [motion], ScreenLayerEffect.None, 1).Single().X, .00001);
        Assert.AreEqual(.5, ScreenLayerAnimation.Sample([source], [motion], ScreenLayerEffect.None, 2.5).Single().Alpha, .00001);
        Assert.IsEmpty(ScreenLayerAnimation.Sample([source], [motion], ScreenLayerEffect.None, 3));
        Assert.IsEmpty(ScreenLayerAnimation.Sample([], [motion], ScreenLayerEffect.None, 1));
    }

    [TestMethod]
    public void LayerEffectsValidateAndRoundTripWithGeometry()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        node.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = "media/" + new string('a', 64) + ".png", morph_key = "one", x = .2, y = .2, width = .5, height = .5, anchor_x = 0, anchor_y = 0, z = 0, enter = new { type = "fade", direction = "left", duration = 1 }, exit = new { type = "slide", direction = "right", duration = 2 } } });
        Assert.IsEmpty(CanonicalSessionPresentationSchema.Validate(node));
        var sprite = ScreenTransitionPreview.Layers(node.Properties["layers"]).Single();
        Assert.AreEqual("fade", sprite.Enter?.Type); Assert.AreEqual("slide", sprite.Exit?.Type);
    }
}
