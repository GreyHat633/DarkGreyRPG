package darkgrey.rpg.story.canonical.runtime;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

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
import darkgrey.rpg.network.message.canonical.CanonicalTitleComplete;
import darkgrey.rpg.network.message.canonical.CanonicalTitleFrame;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class Title0330Probe {

    private Title0330Probe() {}

    public static void main(String[] args) throws Exception {
        darkgrey.rpg.creator.RespawnPresentationCacheProbe.verify();
        CanonicalTitleConfiguration config = new CanonicalTitleConfiguration("第一章", "新的旅程", 1, 2, 1);
        require(
            config.alpha(0) == 0 && config.alpha(0.5) == 0.5f
                && config.alpha(2) == 1
                && config.alpha(3.5) == 0.5f
                && config.alpha(4) == 0,
            "fade timing");
        require(new CanonicalTitleConfiguration("标题", "", 0, 0, 0).alpha(0) == 0, "zero timing completes immediately");
        reject(() -> new CanonicalTitleConfiguration(" ", "", 0, 1, 0));
        reject(() -> new CanonicalTitleConfiguration("x", "", Double.NaN, 1, 0));
        reject(() -> new CanonicalTitleConfiguration("x", "", 0, 61, 0));
        ByteBuf bytes = Unpooled.buffer();
        new CanonicalTitleFrame(9, config).toBytes(bytes);
        CanonicalTitleFrame decoded = new CanonicalTitleFrame();
        decoded.fromBytes(bytes);
        bytes.release();
        require(
            decoded.getToken() == 9 && decoded.getTitle().main.equals("第一章")
                && decoded.getTitle()
                    .duration() == 4,
            "Chinese packet round trip");
        bytes = Unpooled.buffer();
        new CanonicalTitleFrame(9, null).toBytes(bytes);
        decoded.fromBytes(bytes);
        bytes.release();
        require(decoded.getTitle() == null, "clear packet");
        bytes = Unpooled.buffer();
        new CanonicalTitleComplete(9).toBytes(bytes);
        CanonicalTitleComplete ack = new CanonicalTitleComplete();
        ack.fromBytes(bytes);
        bytes.release();
        require(ack.getToken() == 9, "bounded completion receipt");
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("start", "start", "{}"),
            node("title", "title", "{\"main\":\"第一章\",\"subtitle\":\"\",\"fade_in\":1,\"stay\":2,\"fade_out\":1}"),
            node("end", "terminate", "{}"));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (int i = 0; i < nodes.size() - 1; i++) edges.add(
            new CanonicalGraphConnection(
                nodes.get(i)
                    .getId(),
                "flow_out",
                nodes.get(i + 1)
                    .getId(),
                "flow_in",
                CanonicalGraphInterfaceKind.FLOW));
        CanonicalGraphResource resource = new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            "title_story",
            "Title",
            new CanonicalGraph(nodes, edges));
        CanonicalStoryRuntime runtime = CanonicalStoryRuntime
            .start(resource, "flow_out", CanonicalStoryRepeatPolicy.ONCE);
        require(
            runtime.snapshot()
                .getWaitKind() == CanonicalStoryWaitKind.TITLE && runtime.getStatus() == CanonicalStoryStatus.ACTIVE,
            "title blocks Story cursor");
        CanonicalStoryInstanceSnapshot instance = new CanonicalStoryInstanceSnapshot(
            UUID.randomUUID(),
            "title_story",
            1,
            null,
            runtime.snapshot());
        CanonicalStoryInstanceSnapshot saved = CanonicalStoryInstanceNbtCodec
            .decode(CanonicalStoryInstanceNbtCodec.encode(Collections.singletonList(instance)))
            .get(0);
        runtime = CanonicalStoryRuntime.restore(resource, saved.getRuntimeSnapshot());
        require(
            runtime.snapshot()
                .getWaitKind() == CanonicalStoryWaitKind.TITLE,
            "title wait survives reconnect without animation clock");
        final CanonicalStoryRuntime restored = runtime;
        reject(() -> restored.completeTitle("foreign"));
        runtime.completeTitle("title");
        require(runtime.getStatus() == CanonicalStoryStatus.TERMINATED, "completion advances exactly once");
        reject(() -> restored.completeTitle("title"));
        System.out.println("TITLE_0330_PROBE=PASS");
    }

    private static CanonicalGraphNode node(String id, String type, String json) {
        List<CanonicalGraphPort> ports = new ArrayList<CanonicalGraphPort>();
        if (!"start".equals(type)) ports.add(
            new CanonicalGraphPort(
                "flow_in",
                "In",
                CanonicalGraphPortDirection.INPUT,
                CanonicalGraphInterfaceKind.FLOW,
                0));
        if (!"terminate".equals(type)) ports.add(
            new CanonicalGraphPort(
                "flow_out",
                "Out",
                CanonicalGraphPortDirection.OUTPUT,
                CanonicalGraphInterfaceKind.FLOW,
                1));
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : new JsonParser().parse(json)
            .getAsJsonObject()
            .entrySet()) properties.put(entry.getKey(), entry.getValue());
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static void reject(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected rejection");
        } catch (RuntimeException expected) {}
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
