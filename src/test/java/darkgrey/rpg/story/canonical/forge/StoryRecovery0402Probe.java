package darkgrey.rpg.story.canonical.forge;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatch;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Durable target identity, temporary package admission and bounded recursive Flow. */
public final class StoryRecovery0402Probe {

    private static final UUID USER = new UUID(40201, 1);

    private StoryRecovery0402Probe() {}

    public static void main(String[] args) {
        ProjectSnapshot project = Stability0402Fixtures.flow();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        AtomicBoolean enabled = new AtomicBoolean(false);
        CanonicalStoryServerService service = new CanonicalStoryServerService(
            project,
            data,
            story -> !Stability0402Fixtures.TARGET.equals(story) || enabled.get());
        service.startByEntry(USER, Stability0402Fixtures.SOURCE, 402L);
        data.claimStoryTerminalRoute(USER, Stability0402Fixtures.SOURCE, 402L, "out");
        Gateway gateway = new Gateway();
        require(
            !CanonicalStoryForgeManager.recoverPendingTerminalRoutes(USER, service, data, gateway),
            "Disabled target waits");
        require(
            data.getStorySnapshot(USER, Stability0402Fixtures.TARGET) == null
                && data.pendingStoryTerminalRouteStoryIds(USER)
                    .size() == 1,
            "Pending remains durable");
        enabled.set(true);
        require(
            CanonicalStoryForgeManager.recoverPendingTerminalRoutes(USER, service, data, gateway),
            "Reenabled target recovers");
        long run = data.getStorySnapshot(USER, Stability0402Fixtures.TARGET)
            .getActivationTime();
        NBTTagCompound before = image(data);
        data.setDirty(false);
        for (int i = 0; i < 200; i++) require(
            !CanonicalStoryForgeManager.recoverPendingTerminalRoutes(USER, service, data, gateway),
            "Applied is no-op");
        require(
            run == data.getStorySnapshot(USER, Stability0402Fixtures.TARGET)
                .getActivationTime() && before.equals(image(data)) && !data.isDirty(),
            "Applied preserves run, content and dirty");
        require(gateway.starts == 1, "Target dispatch exactly once");
        chain(20, false, false);
        chain(20, true, false);
        chain(300, false, true);
        logicCascade(10, false);
        logicCascade(100, true);
        System.out.println(
            "STORY_RECOVERY_0402=PASS disabled pending reenabled applied idempotent chain cycle complex-Logic/shared-Flow-budget layer=A");
    }

    /** Real public Logic fan-out, double NOT/AND, then two Flow destinations per branch. */
    private static void logicCascade(int branches, boolean overBudget) {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<>();
        Map<String, CanonicalStoryMembership> members = new LinkedHashMap<>();
        List<CanonicalStoryLogicConnection> edges = new ArrayList<>();
        String source = Stability0402Fixtures.loadStory(10000);
        stories.put(source, logicStory(source, false));
        for (int i = 0; i < branches; i++) {
            String target = Stability0402Fixtures.loadStory(11000 + i * 3);
            stories.put(target, logicStory(target, true));
            edges.add(new CanonicalStoryLogicConnection(source, "signal", target, "gate"));
            for (int tail = 1; tail <= 2; tail++) {
                String next = Stability0402Fixtures.loadStory(11000 + i * 3 + tail);
                stories.put(next, Stability0402Fixtures.terminal(next, "out", "flow_driven"));
                edges.add(
                    new CanonicalStoryLogicConnection(
                        tail == 1 ? target : Stability0402Fixtures.loadStory(11000 + i * 3 + 1),
                        "out",
                        next,
                        "out",
                        CanonicalGraphInterfaceKind.FLOW));
            }
        }
        for (String id : stories.keySet()) members.put(
            id,
            new CanonicalStoryMembership(
                id,
                new CanonicalStoryMembershipSet(
                    Collections.emptyList(),
                    Collections.emptyList(),
                    Collections.emptyList())));
        ProjectSnapshot project = new ProjectSnapshot(
            new ProjectDefinition(3, "logic0402", "Logic cascade"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.emptyMap(),
                Collections.emptyMap(),
                members,
                new CanonicalStoryLogicGraph(edges)));
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        service.startByEntry(USER, source, 402L);
        service.setLogicInput(USER, source, "gate", true, 403L);
        Gateway gateway = new Gateway();
        boolean limited = false;
        try {
            CanonicalStoryForgeManager.propagateStoryLogicTrusted(USER, project, service, data, gateway);
        } catch (IllegalStateException expected) {
            require(
                expected.getMessage()
                    .contains("safety bound"),
                "Specific shared Logic/Flow budget failure");
            limited = true;
        }
        require(limited == overBudget, "Logic fan-out bound");
        require(
            gateway.starts <= 256 && (overBudget || gateway.starts == branches * 3),
            "Logic and nested Flow share bound");
        NBTTagCompound saved = image(data);
        CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
        restored.readFromNBT(saved);
        restored.bindProject(project, false);
        require(saved.equals(image(restored)), "Logic budget durable state round-trip");
        if (!overBudget) {
            data.setDirty(false);
            require(
                !CanonicalStoryForgeManager.propagateStoryLogicTrusted(USER, project, service, data, gateway),
                "Stable Logic no-op");
            require(!data.isDirty() && saved.equals(image(data)), "Stable Logic preserves content and dirty");
        }
        require(
            service.startByEntry(new UUID(40201, 2), source, 500L) != null,
            "Unrelated player responds after Logic bound");
    }

    private static CanonicalGraphResource logicStory(String id, boolean triggered) {
        CanonicalGraphResource base = Stability0402Fixtures.terminal(id, "out", triggered ? "logic" : "enter_story");
        List<CanonicalGraphNode> nodes = new ArrayList<>(
            base.getGraph()
                .getNodes());
        List<CanonicalGraphConnection> edges = new ArrayList<>(
            base.getGraph()
                .getConnections());
        if (triggered) {
            CanonicalGraphNode start = nodes.remove(0);
            Map<String, JsonElement> properties = new LinkedHashMap<>(start.getProperties());
            properties.put(
                "triggers",
                new JsonParser().parse(
                    "[{\"port_id\":\"out\",\"display_name\":\"Start\",\"trigger_type\":\"logic\",\"trigger_properties\":{},\"order\":0,\"logic_port_id\":\"gate\"}]"));
            List<CanonicalGraphPort> ports = new ArrayList<>(start.getPorts());
            ports.add(port("gate", CanonicalGraphPortDirection.INPUT, 1));
            nodes.add(0, new CanonicalGraphNode("start", "start", "Start", ports, properties));
        }
        Map<String, JsonElement> named = new LinkedHashMap<>();
        named.put("port_id", new JsonParser().parse("\"gate\""));
        named.put("display_name", new JsonParser().parse("\"Gate\""));
        nodes.add(
            new CanonicalGraphNode(
                "input",
                "logic_input",
                "Input",
                Collections.singletonList(port("logic_out", CanonicalGraphPortDirection.OUTPUT, 0)),
                named));
        if (triggered) edges.add(
            new CanonicalGraphConnection("input", "logic_out", "start", "gate", CanonicalGraphInterfaceKind.LOGIC));
        else {
            for (String node : Arrays.asList("not1", "not2")) nodes.add(
                new CanonicalGraphNode(
                    node,
                    "not",
                    node,
                    Arrays.asList(
                        port("logic_in", CanonicalGraphPortDirection.INPUT, 0),
                        port("logic_out", CanonicalGraphPortDirection.OUTPUT, 1)),
                    Collections.emptyMap()));
            nodes.add(
                new CanonicalGraphNode(
                    "and",
                    "and",
                    "AND",
                    Arrays.asList(
                        port("left", CanonicalGraphPortDirection.INPUT, 0),
                        port("right", CanonicalGraphPortDirection.INPUT, 1),
                        port("logic_out", CanonicalGraphPortDirection.OUTPUT, 2)),
                    Collections.emptyMap()));
            Map<String, JsonElement> output = new LinkedHashMap<>();
            output.put("port_id", new JsonParser().parse("\"signal\""));
            output.put("display_name", new JsonParser().parse("\"Signal\""));
            nodes.add(
                new CanonicalGraphNode(
                    "output",
                    "logic_output",
                    "Output",
                    Collections.singletonList(port("logic_in", CanonicalGraphPortDirection.INPUT, 0)),
                    output));
            edges.add(
                new CanonicalGraphConnection(
                    "input",
                    "logic_out",
                    "not1",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC));
            edges.add(
                new CanonicalGraphConnection(
                    "not1",
                    "logic_out",
                    "not2",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC));
            edges.add(
                new CanonicalGraphConnection("input", "logic_out", "and", "left", CanonicalGraphInterfaceKind.LOGIC));
            edges.add(
                new CanonicalGraphConnection("not2", "logic_out", "and", "right", CanonicalGraphInterfaceKind.LOGIC));
            edges.add(
                new CanonicalGraphConnection(
                    "and",
                    "logic_out",
                    "output",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC));
        }
        return new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.STORY,
            id,
            "Logic",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphPort port(String id, CanonicalGraphPortDirection direction, int order) {
        return new CanonicalGraphPort(id, id, direction, CanonicalGraphInterfaceKind.LOGIC, order);
    }

    private static void chain(int count, boolean loop, boolean overBudget) {
        ProjectSnapshot project = Stability0402Fixtures.flowChain(count, loop);
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        Gateway gateway = new Gateway();
        CanonicalStoryDispatch first = service.startByEntry(USER, Stability0402Fixtures.loadStory(0), 402L);
        boolean limited = false;
        try {
            CanonicalStoryForgeManager.routeTrusted(USER, service, data, first, gateway);
        } catch (IllegalStateException expected) {
            require(
                expected.getMessage()
                    .contains("safety bound"),
                "Specific budget failure");
            limited = true;
        }
        require(limited == overBudget, "Budget boundary");
        require(gateway.starts <= 256 && (overBudget || gateway.starts == count), "One shared bound across recursion");
        NBTTagCompound saved = image(data);
        CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
        restored.readFromNBT(saved);
        restored.bindProject(project, false);
        require(saved.equals(image(restored)), "Budget state round-trip");
        require(
            data.pendingStoryTerminalRouteStoryIds(USER)
                .isEmpty(),
            "Bounded failure does not retry same claims forever");
        UUID unrelated = new UUID(40201, 2);
        require(
            service.startByEntry(unrelated, Stability0402Fixtures.loadStory(0), 500L) != null,
            "Unrelated user still responds");
    }

    private static final class Gateway implements CanonicalStoryForgeManager.AggregateGateway {

        int starts;

        public void storyStarted(CanonicalStoryDispatch dispatch) {
            starts++;
        }

        public boolean startSession(String story, String placement, boolean logic) {
            throw new AssertionError("Unexpected Session");
        }

        public CanonicalTaskInstanceSnapshot startTask(String story, String placement, String resource) {
            throw new AssertionError("Unexpected Task");
        }

        public boolean executeAction(CanonicalStoryDispatch action) {
            throw new AssertionError("Unexpected action");
        }

        public void cleanup(String story) {}

        public void resetPreviousRun(String story) {
            throw new AssertionError("Unexpected restart");
        }
    }

    private static NBTTagCompound image(CanonicalSessionSavedData data) {
        NBTTagCompound result = new NBTTagCompound();
        data.writeToNBT(result);
        return result;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
