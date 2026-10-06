package darkgrey.rpg.client;

import java.io.File;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.session.runtime.CanonicalSessionRuntime;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntime;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Executes Studio-authored and Studio-exported packages, including every eight-input vector. */
public final class LogicInputs0337Probe {

    private LogicInputs0337Probe() {}

    public static void main(String[] args) {
        int resources = 0;
        for (String container : new String[] { "Single", "Group" }) {
            StoryPackageLoader loader = new StoryPackageLoader(
                new File(args[0], container),
                new File(args[1], "Cache-" + container));
            StoryPackageLoader.ReloadResult loaded = loader.load();
            require(
                loaded.isSuccessful(),
                loaded.getErrors()
                    .toString());
            require(
                loader.getPackages()
                    .size() == ("Single".equals(container) ? 1 : 2),
                "package membership");
            for (LoadedStoryPackage pkg : loader.getPackages()
                .values()) {
                List<CanonicalGraphResource> candidates = new ArrayList<CanonicalGraphResource>();
                candidates.add(
                    pkg.getSnapshot()
                        .getCanonicalStory(pkg.getStoryId()));
                candidates.addAll(
                    pkg.getSnapshot()
                        .getCanonicalSessions()
                        .values());
                candidates.addAll(
                    pkg.getSnapshot()
                        .getCanonicalTasks()
                        .values());
                for (CanonicalGraphResource resource : candidates) {
                    boolean fixture = false;
                    for (CanonicalGraphNode node : resource.getGraph()
                        .getNodes()) if ("and_8".equals(node.getId())) fixture = true;
                    if (!fixture) continue;
                    for (int mask = 0; mask < 256; mask++) {
                        Map<String, Boolean> inputs = new LinkedHashMap<String, Boolean>();
                        for (int i = 0; i < 8; i++) inputs.put("in" + i, Boolean.valueOf((mask & (1 << i)) != 0));
                        verify(outputs(resource, inputs, false), mask);
                        verify(outputs(resource, inputs, true), mask);
                    }
                    rejectDamaged(resource);
                    resources++;
                    System.out.println(
                        container + " " + resource.getKind() + ": 256 vectors + restore + unwired + malformed PASS");
                }
            }
        }
        require(resources == 6, "all scopes in both containers");
        System.out.println("LogicInputs0337Probe PASS: 3072 runtime/restore vectors across 6 exported resources");
    }

    private static Map<String, Boolean> outputs(CanonicalGraphResource resource, Map<String, Boolean> inputs,
        boolean restore) {
        switch (resource.getKind()) {
            case STORY:
                CanonicalStoryRuntime story = CanonicalStoryRuntime
                    .start(resource, "entry", CanonicalStoryRepeatPolicy.ONCE, inputs);
                return (restore ? CanonicalStoryRuntime.restore(resource, story.snapshot()) : story)
                    .getPublicLogicOutputs();
            case SESSION:
                CanonicalSessionRuntime session = CanonicalSessionRuntime.start(resource, false, inputs);
                return (restore ? CanonicalSessionRuntime.restore(resource, session.snapshot()) : session)
                    .getPublicLogicOutputs();
            case TASK:
                CanonicalTaskRuntime task = CanonicalTaskRuntime.start(resource);
                for (Map.Entry<String, Boolean> input : inputs.entrySet()) task.setLogicInput(
                    input.getKey(),
                    input.getValue()
                        .booleanValue());
                return (restore ? CanonicalTaskRuntime.restore(resource, task.snapshot()) : task)
                    .getPublicLogicOutputs();
            default:
                throw new AssertionError(resource.getKind());
        }
    }

    private static void verify(Map<String, Boolean> outputs, int mask) {
        for (int count : new int[] { 2, 3, 8 }) {
            int selected = mask & ((1 << count) - 1);
            require(
                Boolean.valueOf(selected == ((1 << count) - 1))
                    .equals(outputs.get("and_" + count)),
                "AND " + count + " mask=" + mask + " outputs=" + outputs);
            require(
                Boolean.valueOf(selected != 0)
                    .equals(outputs.get("or_" + count)),
                "OR " + count + " mask=" + mask);
        }
        require(Boolean.FALSE.equals(outputs.get("and_unwired")), "unconnected input must be false");
    }

    private static void rejectDamaged(CanonicalGraphResource original) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        for (CanonicalGraphNode node : original.getGraph()
            .getNodes()) {
            if (!"and_8".equals(node.getId())) {
                nodes.add(node);
                continue;
            }
            List<CanonicalGraphPort> ports = new ArrayList<CanonicalGraphPort>();
            int inputs = 0;
            for (CanonicalGraphPort port : node.getPorts()) if (!port.isInput() || inputs++ == 0) ports.add(port);
            nodes.add(
                new CanonicalGraphNode(
                    node.getId(),
                    node.getType(),
                    node.getDisplayName(),
                    ports,
                    node.getProperties()));
        }
        CanonicalGraphResource damaged = new CanonicalGraphResource(
            original.getSchemaVersion(),
            original.getKind(),
            original.getId(),
            original.getDisplayName(),
            new CanonicalGraph(
                nodes,
                original.getGraph()
                    .getConnections()),
            original.getTaskMetadata());
        try {
            outputs(damaged, new LinkedHashMap<String, Boolean>(), false);
            throw new AssertionError("malformed gate was accepted");
        } catch (CanonicalGraphResourceException expected) {
            require(
                expected.getCode()
                    .contains("cardinality") || "task.node.ports".equals(expected.getCode()),
                expected.getCode());
        }
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
