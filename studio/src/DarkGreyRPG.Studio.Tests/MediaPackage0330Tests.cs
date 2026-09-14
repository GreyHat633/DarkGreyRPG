using System.IO.Compression;
using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class MediaPackage0330Tests
{
    [TestMethod]
    public void OnlyReachableMediaIsPackagedAndChangedBytesAreRejected()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "media_story", "Media", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        var membership = new CanonicalStoryMembershipManifest("media_story");
        membership.OwnedResources = new CanonicalStoryMembershipSet { Actors = ["hero"] }; store.Memberships.Create(membership);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
        var reference = "media/" + Convert.ToHexStringLower(SHA256.HashData(png)) + ".png";
        var mediaPath = Path.Combine(project.Root, "resources", reference);
        Directory.CreateDirectory(Path.GetDirectoryName(mediaPath)!); File.WriteAllBytes(mediaPath, png);
        var orphan = "media/" + new string('c', 64) + ".png";
        File.WriteAllBytes(Path.Combine(project.Root, "resources", orphan), png);
        var actors = new ActorRepository(project.Root); var actor = actors.CreateIndividual("hero", "Hero");
        actor.HomeStoryId = "media_story"; actor.DefaultPortraitRef = reference;
        actor.SetPortraitVariants([new("default", reference)]); actors.SaveActor(actor);
        var archive = Path.Combine(project.Root, "media_story.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("media_story", archive, "0.3.3.0-P6");
        CollectionAssert.AreEqual(new[] { reference }, result.Manifest.RequiredResources.Media);
        using (var zip = ZipFile.OpenRead(archive))
        {
            Assert.IsNotNull(zip.GetEntry(reference)); Assert.IsNull(zip.GetEntry(orphan));
            Assert.IsFalse(zip.Entries.Any(entry => entry.FullName.Contains("media_sources", StringComparison.Ordinal)));
        }
        var output = Environment.GetEnvironmentVariable("DGR_MEDIA_PACKAGE_FIXTURE");
        if (!string.IsNullOrWhiteSpace(output)) { Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(archive, output, true); }
        File.AppendAllText(mediaPath, "corrupt");
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root).Build("media_story", Path.Combine(project.Root, "bad.dgrs")));
        DgrsPackageValidator.Validate(archive);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        { using var target = zip.CreateEntry(orphan).Open(); target.Write(png); }
        Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(archive));
    }
}
