using System.Security.Cryptography;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class PresentationPackage0330Tests
{
    [TestMethod]
    public void CompleteTitleMusicScreenLineGraphExportsAsOneReachablePackage()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
        string image = "media/" + Convert.ToHexStringLower(SHA256.HashData(png)) + ".png";
        MediaPayloadValidation.Validate(image, png);
        var corrupted = (byte[])png.Clone(); corrupted[45] ^= 1;
        Assert.ThrowsExactly<InvalidDataException>(() => MediaPayloadValidation.Validate(image, corrupted));
        string path = Path.Combine(project.Root, "resources", image);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, png);
        var start = GraphNodeFactory.CreateStoryStart("start");
        var title = GraphNodeFactory.Create(GraphScope.StoryFlow, "title", "title");
        var end = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "terminate");
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "presentation_story", "Presentation", new GraphDocument([start, title, end])));
        var music = GraphNodeFactory.Create(GraphScope.Session, "music", "music");
        var screen = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        screen.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = image, x = 0.5, y = 0.5, width = 0.5, height = 1, anchor_x = 0.5, anchor_y = 0.5, z = 0 } });
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line"); line.Properties["text"] = JsonSerializer.SerializeToElement("完整画面测试");
        var sessionEnd = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        sessionEnd.Properties["port_id"] = JsonSerializer.SerializeToElement("done"); sessionEnd.Properties["display_name"] = JsonSerializer.SerializeToElement("完成");
        var nodes = new[] { GraphNodeFactory.Create(GraphScope.Session, "start", "start"), music, screen, line, sessionEnd };
        var edges = nodes.Zip(nodes.Skip(1), (a, b) => new GraphConnection(a.Id, "flow_out", b.Id, "flow_in", GraphInterfaceKind.Flow));
        store.Sessions.Create(new GraphResourceEnvelope(GraphResourceKind.Session, "presentation_session", "Session", new GraphDocument(nodes, edges)));
        var membership = new CanonicalStoryMembershipManifest("presentation_story") { OwnedResources = new CanonicalStoryMembershipSet { Sessions = ["presentation_session"] } };
        store.Memberships.Create(membership);
        string archive = Path.Combine(project.Root, "presentation_story.dgrs");
        new DgrsStoryPackageExporter(project.Root).Build("presentation_story", archive);
        var fixture = Environment.GetEnvironmentVariable("DGR_PRESENTATION_PACKAGE_FIXTURE");
        if (!string.IsNullOrWhiteSpace(fixture)) { Directory.CreateDirectory(Path.GetDirectoryName(fixture)!); File.Copy(archive, fixture, true); }
    }
}
