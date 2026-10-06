using System.Text.Json;
using System.IO;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels.Graph;

var root = Path.GetFullPath(args[0]);
new ProjectService().CreateProject(root, "acceptance0337", "0.3.3.7 交互验收");
var store = new CanonicalProjectGraphStore(root);
var lifecycle = new CanonicalStoryLifecycleService(store);
var owner = lifecycle.CreateNew("逻辑与交互验收");
var peer = lifecycle.CreateNew("组关系验收");
var resources = new CanonicalStoryResourceLifecycleService(store);
var session = resources.CreateOwned(owner.Id, GraphResourceKind.Session, owner.Id + "~session~logic", "会话逻辑");
var task = resources.CreateOwned(owner.Id, GraphResourceKind.Task, owner.Id + "~task~logic", "任务逻辑");
GraphNode Boundary(GraphScope scope, string type, string id, string boundary)
{
    var node = GraphNodeFactory.Create(scope, type, id);
    node.Properties["port_id"] = JsonSerializer.SerializeToElement(boundary);
    node.Properties["display_name"] = JsonSerializer.SerializeToElement(boundary);
    return node;
}
GraphDocument Logic(GraphScope scope)
{
    var graph = new GraphDocument();
    for (int i = 0; i < 8; i++) graph.Nodes.Add(Boundary(scope, "logic_input", $"input{i}", $"in{i}"));
    foreach (int count in new[] {2,3,8}) foreach (string type in new[] {"and", "or"})
    {
        string name = $"{type}_{count}";
        var gate = new GraphNodeAuthoringService().Create(graph, scope, type, name, $"{(type == "and" ? "与" : "或")} {count} 输入").Candidate!;
        graph.Nodes.Add(gate);
        var host = new GraphEditorHostViewModel(graph, scope);
        using var editor = new CanonicalNodeInspectorViewModel(host,
            host.Nodes.Single(node => node.NodeId == name));
        while (editor.LogicInputs.Count < count) if (!editor.AddLogicInput()) throw new Exception("Could not add input");
        // The authoring host commits cloned documents; retain its final graph.
        graph = editor.Host.Graph;
        gate = graph.Nodes.Single(node => node.Id == name);
        for (int i=0; i<count; i++) graph.Connections.Add(new($"input{i}", "logic_out", name,
            gate.Ports.Where(port => port.IsInput).OrderBy(port => port.Order).ElementAt(i).Id, GraphInterfaceKind.Logic));
        var output = Boundary(scope, "logic_output", "out_"+name, name);
        graph.Nodes.Add(output); graph.Connections.Add(new(name,"logic_out",output.Id,"logic_in",GraphInterfaceKind.Logic));
    }
    // One deliberately unwired third input must remain false for AND.
    var unwired = new GraphNodeAuthoringService().Create(graph,scope,"and","and_unwired").Candidate!;
    unwired.Ports.Add(new("unwired","输入 3",true,GraphInterfaceKind.Logic,2));
    graph.Nodes.Add(unwired); graph.Nodes.Add(Boundary(scope,"logic_output","out_unwired","and_unwired"));
    for(int i=0;i<2;i++) graph.Connections.Add(new($"input{i}","logic_out",unwired.Id,unwired.Ports.Where(port=>port.IsInput).ElementAt(i).Id,GraphInterfaceKind.Logic));
    graph.Connections.Add(new(unwired.Id,"logic_out","out_unwired","logic_in",GraphInterfaceKind.Logic));
    int outputOrder = 0;
    foreach (var output in graph.Nodes.Where(PublicOutputSchema.IsOutput))
        output.Properties["display_order"] = JsonSerializer.SerializeToElement(outputOrder++);
    return graph;
}
var storyGraph = Logic(GraphScope.StoryFlow);
var start = GraphNodeFactory.CreateStoryStart("start",triggerPortId:"entry");
var action = GraphNodeFactory.Create(GraphScope.StoryFlow,"action","wait");
CanonicalStoryActionSchema.TryInitializeType(action, CanonicalStoryActionSchema.SendMessage, out _);
action.Properties["message"]=JsonSerializer.SerializeToElement("逻辑验收等待点");
var stop=Boundary(GraphScope.StoryFlow,"terminate","done","finished");
storyGraph.Nodes.AddRange([start,action,stop]);
storyGraph.Connections.AddRange([new(start.Id,"entry",action.Id,"flow_in",GraphInterfaceKind.Flow),new(action.Id,"flow_out",stop.Id,"flow_in",GraphInterfaceKind.Flow)]);
owner.Graph=storyGraph; store.Stories.Replace(owner);
var sessionGraph=Logic(GraphScope.Session);
var sessionStart=GraphNodeFactory.Create(GraphScope.Session,"start","start");
var line=GraphNodeFactory.Create(GraphScope.Session,"line","line");
var page=CanonicalSessionLineSchema.CreatePage(); page["text"]=JsonSerializer.SerializeToElement("会话逻辑验收");
line.Properties["pages"]=JsonSerializer.SerializeToElement(new[]{page});
var end=Boundary(GraphScope.Session,"end","done","finished");
sessionGraph.Nodes.AddRange([sessionStart,line,end]);
sessionGraph.Connections.AddRange([new("start","flow_out","line","flow_in",GraphInterfaceKind.Flow),new("line","flow_out","done","flow_in",GraphInterfaceKind.Flow)]);
session.Graph=sessionGraph; store.Sessions.Replace(session);
var taskGraph=Logic(GraphScope.Task); taskGraph.Nodes.Add(Boundary(GraphScope.Task,"settle","settle","finished"));
task.Graph=taskGraph; store.Tasks.Replace(task);
for (int index=1; index<=3; index++)
{
    var uiTask=resources.CreateOwned(owner.Id,GraphResourceKind.Task,owner.Id+"~task~ui"+index,"追踪任务 "+index);
    var uiGraph=new GraphDocument();
    for(int objectiveIndex=0;objectiveIndex<(index==1?8:1);objectiveIndex++)
    {
        var objective=GraphNodeFactory.Create(GraphScope.Task,"objective","objective"+objectiveIndex);
        objective.Properties["entity"]=JsonSerializer.SerializeToElement("acceptance_zombie");
        objective.Properties["required"]=JsonSerializer.SerializeToElement(10);
        objective.Properties["description"]=JsonSerializer.SerializeToElement(index==1
            ? "长正文目标 "+(objectiveIndex+1)+"："+string.Concat(Enumerable.Repeat("这是一段需要逐行预算并保留其他任务标题的验收文本。",6))
            : "短目标：消灭目标生物");
        uiGraph.Nodes.Add(objective);
    }
    uiGraph.Nodes.Add(Boundary(GraphScope.Task,"settle","settle","finished"));
    uiTask.Graph=uiGraph;uiTask.TaskMetadata=new("任务独立说明："+index);store.Tasks.Replace(uiTask);
}
var packageRoot=Path.GetFullPath(args[1]); Directory.CreateDirectory(packageRoot);
new DgrsStoryPackageExporter(root).Build(owner.Id,Path.Combine(packageRoot,"Single","fixture.dgrs"),"0.3.3.7");
var peerInput=Boundary(GraphScope.StoryFlow,"logic_input","group_in","from_peer");
var peerGraph=peer.Graph!; peerGraph.Nodes.Add(peerInput); peer.Graph=peerGraph; store.Stories.Replace(peer);
store.StoryLogicGraph.Save([new(owner.Id,"and_3",peer.Id,"from_peer")]);
new DgrsGroupPackageExporter(root).Build(owner.Id,Path.Combine(packageRoot,"Group","fixture.dgrs.g"),"0.3.3.7");
Console.WriteLine(root); Console.WriteLine(owner.Id);
