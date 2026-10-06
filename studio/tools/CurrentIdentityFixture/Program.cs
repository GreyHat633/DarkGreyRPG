using System.Text.Json;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

var root = Path.GetFullPath(args[0]);
if (args.Length > 1 && args[1] == "fingerprint")
{
    var members = DarkGreyRPG.Studio.Core.Packaging.OfflineDgrsPackageReader.ReadContainer(root);
    File.WriteAllText(root + ".fingerprints.json", JsonSerializer.Serialize(members.ToDictionary(member => member.Manifest.StoryId, member => member.Fingerprint)));
    return;
}
new ProjectService().CreateProject(root, "identity_fixture", "当前身份实机验收");
var store = new CanonicalProjectGraphStore(root);
if (args.Length > 1 && args[1] == "runtime-task")
{
    var uid = "ST-AAAA-BBBB-CCCC-DDDD";
    new CanonicalStoryLifecycleService(store).Create(uid, "Runtime Task Reward");
    var taskId = uid + "~task~journey";
    new CanonicalStoryResourceLifecycleService(store).CreateOwned(uid, GraphResourceKind.Task, taskId, "Reach the reward region");
    var definition = store.Tasks.Load(taskId);
    var settle = definition.Graph!.Nodes.Single();
    var resultPort = settle.Ports.Single().Id;
    var goal = GraphNodeFactory.Create(GraphScope.Task, "objective", "reach");
    CanonicalTaskObjectiveSchema.TryInitializeType(goal, CanonicalTaskObjectiveSchema.ReachRegion, out _);
    goal.Properties["description"] = JsonSerializer.SerializeToElement("Reach X=20, Y=4, Z=0 for 7 XP");
    goal.Properties["center_x"] = JsonSerializer.SerializeToElement(20d);
    goal.Properties["center_y"] = JsonSerializer.SerializeToElement(4d);
    goal.Properties["radius"] = JsonSerializer.SerializeToElement(3d);
    var reward = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
    reward.Properties["entries"] = JsonSerializer.SerializeToElement(new[] { new { type = "xp", amount = 7 } });
    definition.Graph = new GraphDocument([settle, goal, reward],
        [new(goal.Id, "logic_status", settle.Id, resultPort, GraphInterfaceKind.Logic),
         new(goal.Id, "logic_status", reward.Id, "logic_in", GraphInterfaceKind.Logic)]);
    store.Tasks.Replace(definition);
    var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry");
    var triggers = System.Text.Json.Nodes.JsonNode.Parse(start.Properties["triggers"].GetRawText())!;
    triggers[0]!["trigger_properties"]!["y"] = 4d;
    triggers[0]!["trigger_properties"]!["radius"] = 8d;
    start.Properties["triggers"] = JsonSerializer.SerializeToElement(triggers);
    var placement = CanonicalAggregateNodeFactory.Create(definition, "task_placement").Candidate!;
    var stop = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "done");
    stop.Properties["port_id"] = JsonSerializer.SerializeToElement("finished");
    stop.Properties["display_name"] = JsonSerializer.SerializeToElement("Reward received");
    var story = store.Stories.Load(uid);
    story.Graph = new GraphDocument([start, placement, stop],
        [new(start.Id, "entry", placement.Id, "flow_in", GraphInterfaceKind.Flow),
         new(placement.Id, resultPort, stop.Id, "flow_in", GraphInterfaceKind.Flow)]);
    store.Stories.Replace(story);
    new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(root).Build(uid, Path.Combine(root, "Packages", "task.dgrs"), "0.3.3.6");
    Console.WriteLine(root);
    return;
}
var owner = StoryUid.Parse("ST-2345-6789-ABCD-EFGH");
var consumer = StoryUid.Parse("ST-JKLM-NPQR-STUV-WXYZ");
var stories = new CanonicalStoryLifecycleService(store);
stories.Create(owner.Value, "资源所有者");
stories.Create(consumer.Value, "引用者");
string Key(ResourceKind kind) => new ResourceAddress(owner, kind, "shared").ToKey();
var actors = new CanonicalStoryActorLifecycleService(store);
actors.CreateOwned(owner.Value, CanonicalStoryActorKind.Individual, Key(ResourceKind.Actor), "测试角色");
actors.AddReference(consumer.Value, Key(ResourceKind.Actor));
var items = new CanonicalStoryItemLifecycleService(store);
items.CreateOwned(owner.Value, CanonicalStoryItemKind.Individual, Key(ResourceKind.Item), "测试物品");
items.CreateOwned(owner.Value, CanonicalStoryItemKind.Collective, Key(ResourceKind.ItemGroup), "测试物品组");
var resources = new CanonicalStoryResourceLifecycleService(store);
resources.CreateOwned(owner.Value, GraphResourceKind.Session, Key(ResourceKind.Session), "测试会话");
resources.CreateOwned(owner.Value, GraphResourceKind.Task, Key(ResourceKind.Task), "测试任务");
var session = store.Sessions.Load(Key(ResourceKind.Session));
var sessionGraph = session.Graph!;
var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
var pages = System.Text.Json.Nodes.JsonNode.Parse(line.Properties["pages"].GetRawText())!;
pages[0]!["text"] = "当前格式会话正文";
line.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(Key(ResourceKind.Actor));
sessionGraph = new GraphDocument(sessionGraph.Nodes.Append(line), sessionGraph.Connections);
session.Graph = sessionGraph;
store.Sessions.Replace(session);
var task = store.Tasks.Load(Key(ResourceKind.Task));
var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
objective.Properties["description"] = JsonSerializer.SerializeToElement("验证角色目标");
objective.Properties["entity"] = JsonSerializer.SerializeToElement(Key(ResourceKind.Actor));
task.Graph = new GraphDocument(task.Graph!.Nodes.Append(objective), task.Graph.Connections);
store.Tasks.Replace(task);
var description = DynamicContentText.Encode([new(Text: "角色："), new(Type: "actor_name", ActorId: Key(ResourceKind.Actor))]);
File.WriteAllText(Path.Combine(root, "dynamic.txt"), description);
Console.WriteLine(root);

new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(root).Build(consumer.Value, Path.Combine(root, "Packages", "consumer.dgrs"), "0.3.3.6");

if (args.Length > 1 && args[1] is "group" or "runtime-group" or "runtime-media" or "runtime-media-three")
{
    if (args[1] is "runtime-group" or "runtime-media" or "runtime-media-three")
    {
        var definition = store.Sessions.Load(Key(ResourceKind.Session));
        var start = GraphNodeFactory.Create(GraphScope.Session, "start", "session_start");
        var lineNode = GraphNodeFactory.Create(GraphScope.Session, "line", "session_line");
        lineNode.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { new { page_id = "runtime_page", text = "0336 running Story A: disable the container, then continue this dialogue." } });
        var end = GraphNodeFactory.Create(GraphScope.Session, "end", "session_end");
        end.Properties["port_id"] = JsonSerializer.SerializeToElement("session_finished");
        end.Properties["display_name"] = JsonSerializer.SerializeToElement("Continue");
        definition.Graph = new GraphDocument([start, lineNode, end],
            [new(start.Id, "flow_out", lineNode.Id, "flow_in", GraphInterfaceKind.Flow), new(lineNode.Id, "flow_out", end.Id, "flow_in", GraphInterfaceKind.Flow)]);
        store.Sessions.Replace(definition);
        foreach (var uid in new[] { owner.Value, consumer.Value })
        {
            var story = store.Stories.Load(uid);
            var storyStart = GraphNodeFactory.CreateStoryStart("story_start", triggerPortId: "entry");
            var triggers = System.Text.Json.Nodes.JsonNode.Parse(storyStart.Properties["triggers"].GetRawText())!;
            triggers[0]!["trigger_properties"]!["x"] = uid == owner.Value ? 0d : 1000d;
            triggers[0]!["trigger_properties"]!["y"] = 4d;
            triggers[0]!["trigger_properties"]!["radius"] = 8d;
            storyStart.Properties["triggers"] = JsonSerializer.SerializeToElement(triggers);
            var stop = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "story_done");
            stop.Properties["port_id"] = JsonSerializer.SerializeToElement("finished");
            stop.Properties["display_name"] = JsonSerializer.SerializeToElement("Completed");
            if (uid == owner.Value)
            {
                var projection = CanonicalAggregateNodeFactory.Create(definition, "session_placement");
                var placement = projection.Candidate ?? throw new InvalidOperationException(string.Join("; ", projection.Issues.Select(issue => issue.Message)));
                story.Graph = new GraphDocument([storyStart, placement, stop],
                    [new(storyStart.Id, "entry", placement.Id, "flow_in", GraphInterfaceKind.Flow), new(placement.Id, "session_finished", stop.Id, "flow_in", GraphInterfaceKind.Flow)]);
            }
            else story.Graph = new GraphDocument([storyStart, stop], [new(storyStart.Id, "entry", stop.Id, "flow_in", GraphInterfaceKind.Flow)]);
            store.Stories.Replace(story);
        }
    }
    var source = store.Stories.Load(owner.Value); var sourceGraph = source.Graph!;
    var output = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "output");
    output.Properties["port_id"] = JsonSerializer.SerializeToElement("group_output");
    output.Properties["display_name"] = JsonSerializer.SerializeToElement("Group output");
    sourceGraph.Nodes.Add(output); source.Graph = sourceGraph; store.Stories.Replace(source);
    var target = store.Stories.Load(consumer.Value); var targetGraph = target.Graph!;
    var input = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "input");
    input.Properties["port_id"] = JsonSerializer.SerializeToElement("group_input");
    input.Properties["display_name"] = JsonSerializer.SerializeToElement("Group input");
    targetGraph.Nodes.Add(input); target.Graph = targetGraph; store.Stories.Replace(target);
    store.StoryLogicGraph.Save([new(owner.Value, "group_output", consumer.Value, "group_input")]);
    if (args[1] is "runtime-media" or "runtime-media-three")
    {
        string InstallMedia(string file)
        {
            var bytes = File.ReadAllBytes(Path.Combine(args[2], file));
            var reference = "media/" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) + Path.GetExtension(file);
            var path = Path.Combine(root, "resources", reference);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return reference;
        }
        var portrait = InstallMedia("portrait.png");
        var voice = InstallMedia("voice.ogg");
        var actorRepository = new DarkGreyRPG.Studio.Core.Actors.ActorRepository(root);
        var actor = actorRepository.LoadActor(Key(ResourceKind.Actor));
        actor.DefaultPortraitRef = portrait;
        actor.SetPortraitVariants([new("default", portrait)]);
        actorRepository.SaveActor(actor);
        var definition = store.Sessions.Load(Key(ResourceKind.Session));
        var graph = definition.Graph!;
        var lineNode = graph.Nodes.Single(node => node.Type == "line");
        lineNode.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(Key(ResourceKind.Actor));
        lineNode.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { new { page_id = "media_page", text = "Media lease acceptance: blue portrait and 20-second tone. Keep this Story active during a sibling update.", voice_ref = voice } });
        definition.Graph = graph;
        store.Sessions.Replace(definition);
    }
    if (args[1] == "runtime-media-three")
    {
        const string third = "ST-EEEE-FFFF-GGGG-HHHH";
        stories.Create(third, "Media C");
        actors.AddReference(third, Key(ResourceKind.Actor));
        var thirdSession = third + "~session~shared";
        resources.CreateOwnedSession(third, thirdSession, "Media C session");
        var original = store.Sessions.Load(Key(ResourceKind.Session));
        var copy = store.Sessions.Load(thirdSession); copy.Graph = original.Graph; store.Sessions.Replace(copy);
        var c = store.Stories.Load(third);
        var start = GraphNodeFactory.CreateStoryStart("story_start", triggerPortId: "entry");
        var triggers = System.Text.Json.Nodes.JsonNode.Parse(start.Properties["triggers"].GetRawText())!;
        triggers[0]!["trigger_properties"]!["x"] = 40d;
        triggers[0]!["trigger_properties"]!["y"] = 4d;
        triggers[0]!["trigger_properties"]!["radius"] = 8d;
        start.Properties["triggers"] = JsonSerializer.SerializeToElement(triggers);
        var placement = CanonicalAggregateNodeFactory.Create(copy, "session_placement").Candidate!;
        var end = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "done");
        end.Properties["port_id"] = JsonSerializer.SerializeToElement("finished");
        end.Properties["display_name"] = JsonSerializer.SerializeToElement("Completed");
        var cOutput = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "output");
        cOutput.Properties["port_id"] = JsonSerializer.SerializeToElement("group_output");
        cOutput.Properties["display_name"] = JsonSerializer.SerializeToElement("Group C output");
        c.Graph = new GraphDocument([start, placement, end, cOutput],
            [new(start.Id, "entry", placement.Id, "flow_in", GraphInterfaceKind.Flow),
             new(placement.Id, "session_finished", end.Id, "flow_in", GraphInterfaceKind.Flow)]);
        store.Stories.Replace(c);
        var b = store.Stories.Load(consumer.Value); var bGraph = b.Graph!;
        var bInput = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "input_c");
        bInput.Properties["port_id"] = JsonSerializer.SerializeToElement("group_input_c");
        bInput.Properties["display_name"] = JsonSerializer.SerializeToElement("Group C input");
        bGraph.Nodes.Add(bInput); b.Graph = bGraph; store.Stories.Replace(b);
        store.StoryLogicGraph.Save([new(owner.Value, "group_output", consumer.Value, "group_input"),
            new(third, "group_output", consumer.Value, "group_input_c")]);
    }
    new DarkGreyRPG.Studio.Core.Packaging.DgrsGroupPackageExporter(root).Build(owner.Value, Path.Combine(root, "Groups", "current.dgrs.g"), "0.3.3.6");
}
