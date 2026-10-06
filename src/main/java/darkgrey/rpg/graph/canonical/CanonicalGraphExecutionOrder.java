package darkgrey.rpg.graph.canonical;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

import com.google.gson.JsonElement;

/** Removes presentation order from snapshot identity while retaining Task result priority. */
public final class CanonicalGraphExecutionOrder {

    private CanonicalGraphExecutionOrder() {}

    public static CanonicalGraphResource normalize(CanonicalGraphResource resource) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            Map<String, JsonElement> properties = new TreeMap<String, JsonElement>(node.getProperties());
            if ("end".equals(node.getType()) || "terminate".equals(node.getType())
                || "logic_output".equals(node.getType())) properties.remove("display_order");
            boolean aggregate = "session".equals(node.getType()) || "task".equals(node.getType())
                || "story".equals(node.getType());
            List<CanonicalGraphPort> ports = new ArrayList<CanonicalGraphPort>();
            for (CanonicalGraphPort port : node.getPorts()) ports.add(
                new CanonicalGraphPort(
                    port.getId(),
                    port.getDisplayName(),
                    port.getDirection(),
                    port.getKind(),
                    aggregate ? 0 : port.getOrder()));
            ports.sort(Comparator.comparing(CanonicalGraphPort::getId));
            nodes.add(new CanonicalGraphNode(node.getId(), node.getType(), node.getDisplayName(), ports, properties));
        }
        nodes.sort(Comparator.comparing(CanonicalGraphNode::getId));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>(
            resource.getGraph()
                .getConnections());
        edges.sort(
            Comparator.comparing(CanonicalGraphConnection::getFromNodeId)
                .thenComparing(CanonicalGraphConnection::getFromPortId)
                .thenComparing(CanonicalGraphConnection::getToNodeId)
                .thenComparing(CanonicalGraphConnection::getToPortId)
                .thenComparing(
                    edge -> edge.getInterfaceKind()
                        .toString()));
        return new CanonicalGraphResource(
            resource.getSchemaVersion(),
            resource.getResourceKind(),
            resource.getId(),
            resource.getDisplayName(),
            new CanonicalGraph(nodes, edges),
            resource.getTaskMetadata());
    }
}
