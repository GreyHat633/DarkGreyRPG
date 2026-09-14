package darkgrey.rpg.graph.canonical;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec;

/** Focused WP-D wire and public-boundary probe; no Minecraft process is required. */
public final class StoryBoundary0331Probe {

    private StoryBoundary0331Probe() {}

    public static void main(String[] args) throws Exception {
        Path file = Files.createTempFile("dgr-wpd-", ".json");
        try {
            Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
            stories.put("a", story("a", "out", null));
            stories.put("b", story("b", null, "in"));
            Files.write(
                file,
                ("{\"schema_version\":2,\"connections\":[" + "{\"source_story_id\":\"a\",\"source_port_id\":\"out\","
                    + "\"target_story_id\":\"b\",\"target_port_id\":\"in\",\"interface_kind\":\"Flow\"}]}")
                        .getBytes(StandardCharsets.UTF_8));
            CanonicalStoryLogicGraph graph = new CanonicalStoryLogicGraphLoader().load(file, stories);
            require(
                graph.getSchemaVersion() == 2 && graph.getConnections()
                    .size() == 1,
                "v2 Flow edge was not loaded");
            require(
                graph.getConnections()
                    .get(0)
                    .getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW,
                "Flow interface kind was not retained");
            Files.write(file, "{\"schema_version\":2,\"connections\":[]}".getBytes(StandardCharsets.UTF_8));
            require(
                new CanonicalStoryLogicGraphLoader().load(file, stories)
                    .getConnections()
                    .isEmpty(),
                "Empty v2 graph was not accepted");
            System.out.println("STORY_BOUNDARY_0331_WIRE=PASS");
            System.out.println("STORY_BOUNDARY_0331_FLOW=PASS");
            faultRecoveryPersistence();
        } finally {
            Files.deleteIfExists(file);
        }
    }

    /**
     * Models the two crash windows around destination start. A claim is only
     * pending before the destination transaction is durable; recovery must see
     * that pending record and retry it. Once destination start has committed,
     * the applied record suppresses a duplicate retry.
     */
    private static void faultRecoveryPersistence() {
        String route = "00000000-0000-0000-0000-000000000001\u0000source\u000010\u0000terminate";
        Map<String, Boolean> observations = new LinkedHashMap<String, Boolean>();
        observations.put("00000000-0000-0000-0000-000000000001\u0000target\u0000flow_in", Boolean.TRUE);
        Map<String, String> routeTargets = new LinkedHashMap<String, String>();
        routeTargets.put(route, "00000000-0000-0000-0000-000000000001\u0000target\u000020");
        NBTTagCompound beforeDestination = CanonicalSessionWorldStateNbtCodec.encode(
            Collections.emptyList(),
            1L,
            Collections.emptyList(),
            Collections.emptyList(),
            Collections.emptyList(),
            observations,
            Collections.singletonList(route),
            routeTargets);
        CanonicalSessionWorldStateNbtCodec.Decoded pending = CanonicalSessionWorldStateNbtCodec
            .decode(beforeDestination);
        require(
            pending.getPendingTerminalRoutes()
                .contains(route),
            "crash before destination start lost pending terminal route");
        require(
            !pending.getTerminalRoutes()
                .contains(route),
            "crash before destination start incorrectly marked route applied");
        require(
            Boolean.TRUE.equals(
                pending.getStartObservations()
                    .get("00000000-0000-0000-0000-000000000001\u0000target\u0000flow_in")),
            "durable start observation was lost during pending recovery");
        require(
            "00000000-0000-0000-0000-000000000001\u0000target\u000020".equals(
                pending.getTerminalRouteTargets()
                    .get(route)),
            "destination run identity was not persisted");

        NBTTagCompound afterDestination = CanonicalSessionWorldStateNbtCodec.encode(
            Collections.emptyList(),
            1L,
            Collections.emptyList(),
            Collections.emptyList(),
            Collections.singletonList(route),
            observations,
            Collections.emptyList(),
            routeTargets);
        CanonicalSessionWorldStateNbtCodec.Decoded applied = CanonicalSessionWorldStateNbtCodec
            .decode(afterDestination);
        require(
            applied.getTerminalRoutes()
                .contains(route),
            "destination-start commit did not persist applied terminal route");
        require(
            !applied.getPendingTerminalRoutes()
                .contains(route),
            "destination-start commit retained pending terminal route");
        System.out.println("STORY_BOUNDARY_0331_FAULT_PENDING_RECOVERY=PASS");
        System.out.println("STORY_BOUNDARY_0331_FAULT_APPLIED_DEDUP=PASS");
    }

    private static CanonicalGraphResource story(String id, String terminatePort, String flowPort) {
        java.util.List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>();
        if (terminatePort != null) {
            Map<String, com.google.gson.JsonElement> props = new LinkedHashMap<String, com.google.gson.JsonElement>();
            props.put("port_id", new JsonPrimitive(terminatePort));
            props.put("display_name", new JsonPrimitive(terminatePort));
            nodes.add(
                new CanonicalGraphNode(
                    "end",
                    "terminate",
                    "End",
                    Arrays.asList(
                        new CanonicalGraphPort(
                            "flow_in",
                            "Flow",
                            CanonicalGraphPortDirection.INPUT,
                            CanonicalGraphInterfaceKind.FLOW,
                            0)),
                    props));
        }
        if (flowPort != null) {
            Map<String, com.google.gson.JsonElement> props = new LinkedHashMap<String, com.google.gson.JsonElement>();
            JsonObject trigger = new JsonObject();
            trigger.addProperty("port_id", flowPort);
            trigger.addProperty("display_name", flowPort);
            trigger.addProperty("trigger_type", "flow_driven");
            trigger.add("trigger_properties", new JsonObject());
            trigger.addProperty("order", 0);
            JsonArray triggers = new JsonArray();
            triggers.add(trigger);
            props.put("repeat_policy", new JsonPrimitive("repeatable"));
            props.put("triggers", triggers);
            nodes.add(
                new CanonicalGraphNode(
                    "start",
                    "start",
                    "Start",
                    Arrays.asList(
                        new CanonicalGraphPort(
                            flowPort,
                            flowPort,
                            CanonicalGraphPortDirection.OUTPUT,
                            CanonicalGraphInterfaceKind.FLOW,
                            0)),
                    props));
        }
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            id,
            id,
            new CanonicalGraph(nodes, Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
