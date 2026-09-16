using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Core.Graphs.Editing;

/// <summary>Detached clipboard contents, with no references to mutable editor state.</summary>
public sealed class GraphClipboardSnapshot
{
    private readonly string _json;
    public GraphScope Scope { get; }
    public GraphClipboardSnapshot(GraphScope scope, GraphDocument graph, IEnumerable<string> selected)
    {
        Scope = scope;
        var ids = selected.ToHashSet(StringComparer.Ordinal);
        _json = new GraphDocument(graph.Nodes.Where(n => ids.Contains(n.Id)),
            graph.Connections.Where(c => ids.Contains(c.FromNodeId) && ids.Contains(c.ToNodeId))).ToJson();
    }
    public GraphDocument Read() => GraphDocument.FromJson(_json);

    public GraphDocument CloneForPaste(GraphScope target, out IReadOnlyDictionary<string, string> nodeIds, bool includeFixed = false)
    {
        if (Scope == GraphScope.Project || target == GraphScope.Project) throw new InvalidOperationException("项目图谱中的故事节点不支持复制。");
        var graph = Read();
        foreach (var node in graph.Nodes)
        {
            if (!GraphNodeDefinitionRegistry.TryGet(target, node.Type, out var definition))
                throw new InvalidOperationException($"目标图不支持节点：{node.DisplayName}。");
            if (!includeFixed && (definition.Unique || definition.Required))
                throw new InvalidOperationException($"{node.DisplayName} 是固定节点，已拒绝整次粘贴。");
        }
        var ids = graph.Nodes.ToDictionary(n => n.Id, _ => $"node_{Guid.NewGuid():N}", StringComparer.Ordinal);
        var ports = new Dictionary<(string Node, string Port), string>();
        foreach (var node in graph.Nodes)
        {
            if (node.Type == "line")
            {
                if (CanonicalSessionLineSchema.Validate(node).Count != 0)
                    throw new InvalidOperationException("台词句子数据无效，无法粘贴。请先修复问题列表中的台词错误。");
                var pages = CanonicalSessionLineSchema.ReadPages(node);
                foreach (var page in pages) page["page_id"] = JsonSerializer.SerializeToElement($"page_{Guid.NewGuid():N}");
                node.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
                CanonicalSessionLineSchema.Normalize(node);
            }
            var fixedIds = GraphNodeDefinitionRegistry.Get(target, node.Type)!.FixedPorts.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            var dynamicIds = node.Ports.Where(p => !fixedIds.Contains(p.Id)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            if (node.Properties.TryGetValue("port_id", out var boundary) && boundary.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(boundary.GetString())) dynamicIds.Add(boundary.GetString()!);
            var replacements = dynamicIds.ToDictionary(id => id, _ => $"port_{Guid.NewGuid():N}", StringComparer.Ordinal);
            foreach (var port in node.Ports)
            {
                string old = port.Id;
                port.Id = replacements.GetValueOrDefault(old, old);
                ports[(node.Id, old)] = port.Id;
            }
            foreach (var key in node.Properties.Keys.ToArray())
            {
                var value = JsonNode.Parse(node.Properties[key].GetRawText());
                Rewrite(value, replacements);
                if (key == "port_id" && value is JsonValue text && text.TryGetValue<string>(out var id) && replacements.TryGetValue(id, out var replacement)) value = JsonValue.Create(replacement);
                node.Properties[key] = JsonSerializer.SerializeToElement(value);
            }
        }
        foreach (var edge in graph.Connections)
        {
            edge.FromPortId = ports[(edge.FromNodeId, edge.FromPortId)];
            edge.ToPortId = ports[(edge.ToNodeId, edge.ToPortId)];
            edge.FromNodeId = ids[edge.FromNodeId]; edge.ToNodeId = ids[edge.ToNodeId];
        }
        foreach (var node in graph.Nodes) node.Id = ids[node.Id];
        nodeIds = ids;
        return graph;
    }

    private static void Rewrite(JsonNode? node, IReadOnlyDictionary<string, string> ids)
    {
        if (node is JsonObject obj)
            foreach (var property in obj.ToArray())
            {
                if (property.Key is "port_id" or "id" && property.Value is JsonValue value
                    && value.TryGetValue<string>(out var id) && ids.TryGetValue(id, out var mapped)) obj[property.Key] = mapped;
                else Rewrite(property.Value, ids);
            }
        else if (node is JsonArray array) foreach (var item in array) Rewrite(item, ids);
    }

    public static GraphDocument PasteParameters(GraphScope scope, GraphDocument source, GraphNode parameters, string targetId,
        out IReadOnlyList<GraphConnection> removed, bool preserveProjectedPorts = false)
    {
        var graph = GraphDocument.FromJson(source.ToJson());
        var target = graph.Nodes.Single(n => n.Id == targetId);
        if (target.Type != parameters.Type) throw new InvalidOperationException("只能向同类型节点粘贴参数。");
        var replacement = preserveProjectedPorts
            ? GraphDocument.FromJson(new GraphDocument([parameters]).ToJson()).Nodes.Single()
            : new GraphClipboardSnapshot(scope, new GraphDocument([parameters]), [parameters.Id]).CloneForPaste(scope, out _, true).Nodes.Single();
        replacement.Id = target.Id;
        var originalPortIds = parameters.Ports.Select((p, i) => (p.Id, New: replacement.Ports[i].Id)).ToDictionary(p => p.Id, p => p.New);
        var retainedIds = new Dictionary<string, string>();
        var mapping = new Dictionary<string, string>();
        var matchedCandidates = new Dictionary<string, string>();
        foreach (var port in target.Ports)
        {
            var candidates = replacement.Ports.Where(p => (p.Id == port.Id || originalPortIds.GetValueOrDefault(port.Id) == p.Id) && p.Direction == port.Direction && p.Kind == port.Kind).ToArray();
            if (candidates.Length == 0)
            {
                candidates = replacement.Ports.Where(p => p.DisplayName == port.DisplayName && p.Direction == port.Direction && p.Kind == port.Kind).ToArray();
                if (target.Ports.Count(p => p.DisplayName == port.DisplayName && p.Direction == port.Direction && p.Kind == port.Kind) != 1) candidates = [];
            }
            if (candidates.Length == 1)
            {
                matchedCandidates[port.Id] = candidates[0].Id;
                mapping[port.Id] = preserveProjectedPorts ? candidates[0].Id : port.Id;
                if (!preserveProjectedPorts) retainedIds[candidates[0].Id] = port.Id;
            }
        }
        foreach (var collision in matchedCandidates.GroupBy(p => p.Value).Where(g => g.Count() > 1))
        {
            retainedIds.Remove(collision.Key);
            foreach (var pair in collision) mapping.Remove(pair.Key);
        }
        if (!preserveProjectedPorts)
        {
            foreach (var port in replacement.Ports) port.Id = retainedIds.GetValueOrDefault(port.Id, port.Id);
            foreach (var key in replacement.Properties.Keys.ToArray())
            {
                var value = JsonNode.Parse(replacement.Properties[key].GetRawText());
                Rewrite(value, retainedIds);
                replacement.Properties[key] = JsonSerializer.SerializeToElement(value);
            }
            if (target.Properties.TryGetValue("port_id", out var boundary)) replacement.Properties["port_id"] = boundary.Clone();
        }
        var lost = graph.Connections.Where(c => c.FromNodeId == targetId && !mapping.ContainsKey(c.FromPortId)
            || c.ToNodeId == targetId && !mapping.ContainsKey(c.ToPortId)).ToArray();
        foreach (var edge in lost) graph.Connections.Remove(edge);
        foreach (var edge in graph.Connections)
        {
            if (edge.FromNodeId == targetId) edge.FromPortId = mapping[edge.FromPortId];
            if (edge.ToNodeId == targetId) edge.ToPortId = mapping[edge.ToPortId];
        }
        graph.Nodes[graph.Nodes.IndexOf(target)] = replacement;
        removed = lost;
        return graph;
    }
}
