using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class SessionPortrait0330Tests
{
    private static string Image => "media/" + new string('a', 64) + ".png";

    [TestMethod]
    public void NullableSpeakerAndVoiceShareOneLineSchema()
    {
        Assert.IsNull(GraphNodeDefinitionRegistry.Get(GraphScope.Session, "narration"));
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        Assert.AreEqual(JsonValueKind.Null, line.Properties["speaker_actor_id"].ValueKind);
        line.Properties["voice_ref"] = JsonSerializer.SerializeToElement("media/" + new string('b', 64) + ".ogg");
        Assert.AreEqual(0, CanonicalSessionLineSchema.Validate(line).Count);
        line.Properties["voice_ref"] = JsonSerializer.SerializeToElement("E:/outside.mp3");
        Assert.IsTrue(CanonicalSessionLineSchema.Validate(line).Count > 0);
        line.Properties["voice_ref"] = JsonSerializer.SerializeToElement<string?>(null);
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(123);
        Assert.IsTrue(CanonicalSessionLineSchema.Validate(line).Count > 0);
    }

    [TestMethod]
    public void PortraitsRoundTripRenameAndDocumentSaveWithoutIdentityChanges()
    {
        var actor = new IndividualActorResource { NpcId = "hero", DisplayName = "Hero", HomeStoryId = "intro",
            DefaultPortraitRef = Image, PortraitVariants = [new("开心 / happy", Image)] };
        var json = ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource);
        var reopened = ActorSerializer.DeserializeIndividual(json);
        Assert.AreEqual(4, reopened.SchemaVersion);
        Assert.AreEqual(Image, ActorPortraitSchema.Resolve(reopened, null));
        Assert.AreEqual(Image, ActorPortraitSchema.Resolve(reopened, "开心 / happy"));
        Assert.ThrowsExactly<InvalidOperationException>(() => ActorPortraitSchema.Resolve(reopened, "missing"));
        var renamed = reopened.WithId("other");
        Assert.AreEqual("other", renamed.NpcId);
        Assert.AreEqual(Image, renamed.DefaultPortraitRef);
        Assert.AreEqual("开心 / happy", renamed.PortraitVariants.Single().Name);
        var document = ActorDocument.FromResource(reopened, Path.Combine(Path.GetTempPath(), "hero.json"));
        document.DisplayName = "Changed";
        Assert.AreEqual(Image, document.ToResource().DefaultPortraitRef);
        Assert.AreEqual(1, document.ToResource().PortraitVariants.Count);
        document.SetPortraitVariants([new("duplicate", Image), new("duplicate", Image)]);
        Assert.IsTrue(document.ValidationErrors.Any(issue => issue.Code == "actor.portrait.name"));
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(json.Replace("\"schema_version\": 4", "\"schema_version\": 3")));
    }
}
