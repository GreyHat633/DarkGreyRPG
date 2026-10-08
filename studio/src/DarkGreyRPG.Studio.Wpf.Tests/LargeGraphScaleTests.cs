using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class LargeGraphScaleTests
{
    [TestMethod]
    public void CurrentGraphBuildsTwoHundredNodesAndFourHundredConnections()
    {
        var nodes = Enumerable.Range(0,200).Select(index=>GraphNodeFactory.Create(GraphScope.StoryFlow,"flow_judgment", $"branch_{index}")).ToArray();
        var connections=Enumerable.Range(0,200).SelectMany(index=>new[]{
            new GraphConnection($"branch_{index}","true",$"branch_{(index+1)%200}","flow_in",GraphInterfaceKind.Flow),
            new GraphConnection($"branch_{index}","false",$"branch_{(index+2)%200}","flow_in",GraphInterfaceKind.Flow)});
        var host=new GraphEditorHostViewModel(new GraphDocument(nodes,connections),GraphScope.StoryFlow);
        Assert.HasCount(200,host.Nodes);Assert.HasCount(400,host.Connections);
    }

    [TestMethod]
    public void CurrentProjectGraphLaysOutTwoHundredStoriesAndFourHundredRelationsInOneScc()
    {
        var ids=Enumerable.Range(0,200).Select(i=>"story_"+i).ToArray();
        var snapshot=new CanonicalProjectStoryGraphSnapshot(
            ids.Select(id=>new CanonicalProjectStoryGraphNode(id,id,true,true,true,true,false,[])),
            ids.Select((id,i)=>new CanonicalProjectStoryGraphEdge(id,ids[(i+1)%200],
                [new(id,ids[(i+1)%200],"external_a"),new(id,ids[(i+1)%200],"external_b")])),[]);
        var graph=new ProjectGraphViewModel(snapshot);graph.AutoLayout();
        Assert.HasCount(200,graph.Nodes);Assert.HasCount(200,graph.Edges);
        Assert.AreEqual(400,graph.Edges.Sum(edge=>edge.Count));
        Assert.IsTrue(graph.Nodes.All(node=>double.IsFinite(node.X)&&double.IsFinite(node.Y)));
        Assert.HasCount(200,graph.Diagnostics.Where(issue=>issue.Code=="project_graph.story.cycle"));
    }
}
