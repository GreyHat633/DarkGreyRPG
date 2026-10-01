package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTSizeTracker;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.creator.CreatorSnapshot;
import darkgrey.rpg.creator.TaskSnapshotTransport;
import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation;
import darkgrey.rpg.session.runtime.CanonicalSessionRuntime;
import darkgrey.rpg.session.runtime.CanonicalSessionStatus;
import darkgrey.rpg.session.server.SessionTextLimit;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class Construction0335Probe {

    private static void storyGrouping() {
        NBTTagList tasks = new NBTTagList();
        for (int i = 0; i < 3; i++) {
            NBTTagCompound task = new NBTTagCompound();
            task.setString("id", "task" + i);
            task.setString("story", i < 2 ? "a" : "b");
            task.setString("story_title", "同名故事");
            task.setString("story_package", i < 2 ? "包甲" : "包乙");
            tasks.appendTag(task);
        }
        java.util.Set<String> expanded = new java.util.HashSet<String>();
        List<NBTTagCompound> rows = darkgrey.rpg.client.gui.TaskStoryRows.flatten(tasks, expanded);
        require(rows.size() == 2, "initial story-only view");
        require(
            !rows.get(0)
                .getString("title")
                .equals(
                    rows.get(1)
                        .getString("title")),
            "same-name story disambiguation");
        expanded.add("a");
        rows = darkgrey.rpg.client.gui.TaskStoryRows.flatten(tasks, expanded);
        require(
            rows.size() == 4 && rows.get(1)
                .getString("id")
                .equals("task0"),
            "expand only chosen story");
        NBTTagCompound old = new NBTTagCompound();
        old.setString("id", "history:1:p5:story1:n1:r");
        darkgrey.rpg.creator.TaskStoryPresentation.recover(old);
        require("story".equals(old.getString("story")), "legacy story identity recovery");
        NBTTagCompound orphan = new NBTTagCompound();
        orphan.setString("id", "old");
        tasks = new NBTTagList();
        tasks.appendTag(orphan);
        require(
            "未归属故事".equals(
                darkgrey.rpg.client.gui.TaskStoryRows.flatten(tasks, expanded)
                    .get(0)
                    .getString("title")),
            "orphan remains visible");
        System.out.println("STORY_GROUPING_AND_LEGACY_HISTORY=PASS");
    }

    private static final String IMAGE = "media/" + repeat('a', 64) + ".png",
        OTHER = "media/" + repeat('b', 64) + ".png";

    public static void main(String[] args) throws Exception {
        storyGrouping();
        orderedImageSequences();
        independentImageEffects();
        capacityGeometry();
        CanonicalSessionPresentation a = CanonicalSessionPresentation.EMPTY.apply(screen("a", IMAGE, "key", 0, .2));
        CanonicalSessionPresentation b = a.apply(screen("b", OTHER, "key", .8, .6));
        ScreenTransition motion = new ScreenTransition();
        motion.retarget(a.getScreenRevision(), a.getLayers(), a.getTransition(), 0, false);
        motion.retarget(b.getScreenRevision(), b.getLayers(), b.getTransition(), 0, true);
        List<ScreenTransition.Sprite> mid = motion.sample(500000000L);
        require(
            mid.size() == 2 && Math.abs(mid.get(0).x - .4) < 1e-9 && Math.abs(mid.get(0).w - .4) < 1e-9,
            "per-layer rectangle/texture crossfade");
        require(
            !motion.retarget(b.getScreenRevision(), b.getLayers(), b.getTransition(), 600000000L, true),
            "retransmit must not replay");
        CanonicalSessionPresentation c = b.apply(screen("c", IMAGE, "key", .1, .3));
        motion.retarget(c.getScreenRevision(), c.getLayers(), c.getTransition(), 500000000L, true);
        require(
            Math.abs(
                motion.sample(500000000L)
                    .get(0).x - .4)
                < 1e-9,
            "interruption captures actually displayed source");
        require(
            motion.sample(1500000000L)
                .size() == 1
                && motion.refs(1500000000L)
                    .size() == 1,
            "exact final target and source release");
        reject(
            () -> CanonicalSessionPresentation.fromJson(
                a.toJson()
                    .replace("\"screen_revision\":1", "\"screen_revision\":99")));

        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(choice());
        long epoch = runtime.snapshot()
            .getLineEpoch();
        require(
            !runtime.getCurrentStep()
                .getOptions()
                .get(0)
                .isVisible()
                && !runtime.getCurrentStep()
                    .getOptions()
                    .get(1)
                    .isEnabled(),
            "false hides/disables");
        runtime.choose("b");
        require(
            runtime.snapshot()
                .getSelectedOptionIds()
                .isEmpty(),
            "disabled click inert");
        runtime.setLogicInput("gate", true);
        require(
            runtime.snapshot()
                .getLineEpoch() == epoch && runtime.getCurrentStep()
                    .getOptions()
                    .get(1)
                    .isEnabled(),
            "availability refresh independent epoch");
        CanonicalSessionSavedData saved = new CanonicalSessionSavedData();
        saved.bind(id -> choice());
        CanonicalSessionInstanceSnapshot instance = saved.start(UUID.randomUUID(), "story", "placement", choice());
        reject(() -> saved.presentationText(instance, "option:a", () -> repeat('界', 683)));
        require(
            "legal".equals(saved.presentationText(instance, "option:a", () -> "legal")),
            "overflow never froze a substitute");
        require("legal".equals(saved.presentationText(instance, "option:a", () -> "changed")), "legal option frozen");
        SessionTextLimit.require(repeat('界', 682), "option:a", 2048);
        runtime.choose("b");
        require(runtime.getStatus() == CanonicalSessionStatus.COMPLETED, "enabled selection succeeds");

        NBTTagCompound snapshot = new NBTTagCompound();
        snapshot.setLong("revision", 88);
        snapshot.setInteger("dimension", 0);
        NBTTagList tasks = new NBTTagList();
        for (int i = 0; i < 1000; i++) {
            NBTTagCompound task = new NBTTagCompound();
            task.setString("id", "task" + i);
            task.setString("title", repeat('界', 100));
            task.setTag("objectives", new NBTTagList());
            tasks.appendTag(task);
        }
        snapshot.setTag("tasks", tasks);
        List<CreatorSnapshot> packets = TaskSnapshotTransport.encode(snapshot);
        require(packets.size() > 1, "complete snapshot splits");
        java.io.ByteArrayOutputStream combined = new java.io.ByteArrayOutputStream();
        for (CreatorSnapshot part : packets) {
            ByteBuf wire = Unpooled.buffer();
            part.toBytes(wire);
            CreatorSnapshot decoded = new CreatorSnapshot();
            decoded.fromBytes(wire);
            wire.release();
            combined.write(decoded.data.getByteArray("payload"));
        }
        NBTTagCompound decoded = CompressedStreamTools.func_152456_a(
            new java.io.DataInputStream(new java.io.ByteArrayInputStream(combined.toByteArray())),
            new NBTSizeTracker(64 * 1024 * 1024));
        require(snapshot.equals(decoded), "all task rows preserved across bounded envelopes");
        java.lang.reflect.Method assemble = darkgrey.rpg.client.CanonicalTaskClientStore.class
            .getDeclaredMethod("assemble", NBTTagCompound.class);
        assemble.setAccessible(true);
        NBTTagCompound assembled = null;
        for (int i = packets.size() - 1; i >= 0; i--) {
            assembled = (NBTTagCompound) assemble.invoke(null, packets.get(i).data);
            if (i > 0) require(assembled == null, "partial snapshot never published");
        }
        require(snapshot.equals(assembled), "out-of-order snapshot assembles atomically");
        NBTTagCompound history = new NBTTagCompound();
        history.setString("content_source", "snapshot");
        NBTTagList objectives = new NBTTagList();
        for (int i = 0; i < 2000; i++) {
            NBTTagCompound goal = new NBTTagCompound();
            goal.setString("text", "Goal " + i + repeat('界', 1200));
            objectives.appendTag(goal);
        }
        history.setTag("objectives", objectives);
        int visited = 0;
        while (visited < objectives.tagCount()) {
            NBTTagCompound page = darkgrey.rpg.creator.CanonicalTaskHistoryProjection.details(history, visited);
            require(darkgrey.rpg.creator.TaskCandidateIndex.measured(page) <= 100000, "history detail byte budget");
            NBTTagList rows = page.getCompoundTag("record")
                .getTagList("objectives", 10);
            require(rows.tagCount() <= 20 && page.getInteger("next") > visited, "history page progresses");
            for (int i = 0; i < rows.tagCount(); i++) require(
                rows.getCompoundTagAt(i)
                    .equals(objectives.getCompoundTagAt(visited + i)),
                "history target never omitted");
            visited = page.getInteger("next");
        }
        require(
            history.getTagList("objectives", 10)
                .tagCount() == 2000,
            "history storage untouched");
        candidatePages();
        System.out.println("CONSTRUCTION_0335_SCREEN_CHOICE_TEXT_SNAPSHOT=PASS");
    }

    private static void candidatePages() throws Exception {
        java.lang.reflect.Field repository = darkgrey.rpg.DarkGreyRpg.class.getDeclaredField("projectRepository");
        repository.setAccessible(true);
        repository.set(
            null,
            new darkgrey.rpg.project.ProjectRepository(new java.io.File(".tooling/0335/CandidateProbeProject")));
        java.lang.reflect.Method register = net.minecraft.item.Item.itemRegistry.getClass()
            .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
        register.setAccessible(true);
        register.invoke(net.minecraft.item.Item.itemRegistry, 31001, "probe:item", new net.minecraft.item.Item());
        darkgrey.rpg.item.identity.ItemIdentitySavedData bindings = new darkgrey.rpg.item.identity.ItemIdentitySavedData();
        for (int i = 0; i < 121; i++) {
            NBTTagCompound tag = new NBTTagCompound();
            tag.setString("value", "candidate" + i + repeat('a', i == 120 ? 9000 : 1800));
            bindings.addGroupMember(
                "probe:group",
                new darkgrey.rpg.item.identity.ItemGroupMember(
                    darkgrey.rpg.item.identity.ItemMatchMode.EXACT,
                    new darkgrey.rpg.item.identity.ItemStackDefinition("probe:item", i, tag)));
        }
        CanonicalGraphNode objective = node(
            "objective",
            "objective",
            "{\"objective_type\":\"collect_item\",\"item\":\"probe:group\",\"metadata\":{}}");
        NBTTagCompound summary = darkgrey.rpg.creator.TaskCandidateIndex.summary(objective, bindings);
        require(
            summary.getBoolean("group") && summary.getInteger("total") == 121
                && summary.getTagList("items", 10)
                    .tagCount() == 1,
            "summary holds one candidate");
        int cursor = 0;
        while (cursor < 121) {
            NBTTagCompound page = darkgrey.rpg.creator.TaskCandidateIndex.page(objective, bindings, cursor, 20);
            require(
                page.getTagList("items", 10)
                    .tagCount() <= 20 && darkgrey.rpg.creator.TaskCandidateIndex.measured(page) < 66560,
                "candidate row and byte bounds");
            if (page.getInteger("next") == 121) require(
                page.getTagList("items", 10)
                    .getCompoundTagAt(
                        page.getTagList("items", 10)
                            .tagCount() - 1)
                    .hasKey("error", 8),
                "oversized NBT visible placeholder");
            require(page.getInteger("next") > cursor, "finite candidate cursor progresses");
            cursor = page.getInteger("next");
        }
        bindings.releaseGroup("probe:group");
        require(
            darkgrey.rpg.creator.TaskCandidateIndex.summary(objective, bindings)
                .getInteger("total") == 0,
            "binding revision invalidates descriptors");
    }

    private static void capacityGeometry() throws Exception {
        com.google.gson.JsonObject profile = new JsonParser()
            .parse(
                new String(
                    java.nio.file.Files.readAllBytes(java.nio.file.Paths.get("schema/dialogue-capacity-profile.json")),
                    java.nio.charset.StandardCharsets.UTF_8))
            .getAsJsonObject();
        com.google.gson.JsonArray shapes = profile.getAsJsonArray("geometries");
        for (JsonElement element : shapes) {
            com.google.gson.JsonObject shape = element.getAsJsonObject();
            double scale = darkgrey.rpg.client.session.DialogueFontScale.effective(
                1.5,
                shape.get("gui_factor")
                    .getAsInt());
            darkgrey.rpg.client.session.CanonicalDialogueLayout layout = new darkgrey.rpg.client.session.CanonicalDialogueLayout(
                shape.get("width")
                    .getAsInt(),
                shape.get("height")
                    .getAsInt(),
                scale,
                9);
            int wrap = Math.max(1, (int) ((layout.textWidth - 2 * Math.ceil(9 * scale)) / scale));
            int rows = Math.max(1, (layout.bodyBottom() - layout.bodyTop()) / (int) Math.ceil(9 * scale));
            require(
                wrap == shape.get("wrap_width")
                    .getAsInt() && rows
                        == shape.get("rows")
                            .getAsInt(),
                "capacity profile uses production dialogue geometry");
            require(
                shape.get("safe")
                    .getAsInt() == (int) Math.floor(wrap * rows * .9),
                "exactly one safety margin");
        }
        for (int factor = 1; factor <= 32; factor++) {
            double scale = darkgrey.rpg.client.session.DialogueFontScale.effective(1.5, factor);
            for (int height = 240; height <= 2160; height++) {
                darkgrey.rpg.client.session.CanonicalDialogueLayout layout = new darkgrey.rpg.client.session.CanonicalDialogueLayout(
                    320,
                    height,
                    scale,
                    9);
                int wrap = Math.max(1, (int) ((layout.textWidth - 2 * Math.ceil(9 * scale)) / scale));
                int rows = Math.max(1, (layout.bodyBottom() - layout.bodyTop()) / (int) Math.ceil(9 * scale));
                boolean covered = false;
                for (JsonElement element : shapes) {
                    com.google.gson.JsonObject shape = element.getAsJsonObject();
                    covered |= shape.get("wrap_width")
                        .getAsInt() <= wrap
                        && shape.get("rows")
                            .getAsInt() <= rows;
                }
                require(covered, "profile covers scaled-resolution geometry envelope");
            }
        }
    }

    private static CanonicalGraphResource choice() {
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("start", "start", "{}", port("flow_out", false, false)),
            node(
                "gate",
                "logic_input",
                "{\"port_id\":\"gate\",\"display_name\":\"Gate\"}",
                port("logic_out", false, true)),
            node(
                "choice",
                "choice",
                "{\"prompt\":\"Pick\",\"options\":[{\"option_id\":\"a\",\"display_text\":\"A\",\"flow_port_id\":\"a_flow\",\"condition_port_id\":\"a_condition\",\"unavailable_behavior\":\"hide\",\"unavailable_hint\":\"\"},{\"option_id\":\"b\",\"display_text\":\"B\",\"flow_port_id\":\"b_flow\",\"condition_port_id\":\"b_condition\",\"unavailable_behavior\":\"disable\",\"unavailable_hint\":\"Wait\"}]}",
                port("flow_in", true, false),
                port("a_flow", false, false),
                port("b_flow", false, false),
                port("a_condition", true, true),
                port("b_condition", true, true)),
            node("end", "end", "{\"port_id\":\"done\",\"display_name\":\"Done\"}", port("flow_in", true, false)));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            "choice",
            "Choice",
            new CanonicalGraph(
                nodes,
                Arrays.asList(
                    edge("start", "flow_out", "choice", "flow_in", false),
                    edge("choice", "a_flow", "end", "flow_in", false),
                    edge("choice", "b_flow", "end", "flow_in", false),
                    edge("gate", "logic_out", "choice", "a_condition", true),
                    edge("gate", "logic_out", "choice", "b_condition", true))));
    }

    private static CanonicalGraphConnection edge(String a, String ap, String b, String bp, boolean logic) {
        return new CanonicalGraphConnection(
            a,
            ap,
            b,
            bp,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW);
    }

    private static CanonicalGraphPort port(String id, boolean input, boolean logic) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static void orderedImageSequences() {
        JsonObject layer = new JsonParser().parse(imageLayer(IMAGE, "sequence", 1, 1))
            .getAsJsonObject();
        layer.remove("enter");
        layer.remove("exit");
        layer.addProperty("morph_duration", 0);
        JsonArray steps = new JsonParser().parse(
            "[{\"kind\":\"enter\",\"type\":\"fade\",\"direction\":\"left\",\"duration\":1,\"delay\":1},{\"kind\":\"exit\",\"type\":\"fade\",\"direction\":\"left\",\"duration\":1,\"delay\":2}]")
            .getAsJsonArray();
        layer.add("animations", steps);
        CanonicalSessionPresentation state = CanonicalSessionPresentation.EMPTY
            .apply(node("sequence", "screen", "{\"layers\":[" + layer + "]}"));
        CanonicalSessionPresentation restored = CanonicalSessionPresentation.fromJson(state.toJson());
        JsonObject oversized = new JsonParser().parse(layer.toString())
            .getAsJsonObject();
        JsonArray excessiveSteps = new JsonArray();
        for (int i = 0; i < 500; i++) excessiveSteps.add(steps.get(0));
        oversized.add("animations", excessiveSteps);
        boolean rejected = false;
        try {
            CanonicalSessionPresentation.EMPTY.apply(node("too-large", "screen", "{\"layers\":[" + oversized + "]}"));
        } catch (IllegalArgumentException expected) {
            rejected = expected.getMessage()
                .contains("transmission capacity");
        }
        require(rejected, "oversized animations must fail explicitly, never truncate");
        require(
            restored.getLayers()
                .get(0).animations.size() == 2,
            "sequence roundtrip");
        ScreenTransition timeline = new ScreenTransition();
        timeline.retarget(state.getScreenRevision(), state.getLayers(), state.getTransition(), 0, true);
        require(
            timeline.sample(500000000L)
                .isEmpty(),
            "enter delay starts hidden");
        require(
            Math.abs(
                timeline.sample(1500000000L)
                    .get(0).alpha - .5)
                < 1e-9,
            "entry midpoint");
        require(
            timeline.sample(3000000000L)
                .get(0).alpha == 1,
            "exit delay preserves visible state");
        require(
            Math.abs(
                timeline.sample(4500000000L)
                    .get(0).alpha - .5)
                < 1e-9,
            "exit midpoint");
        require(
            timeline.sample(6000000000L)
                .isEmpty()
                && timeline.sample(9000000000L)
                    .isEmpty(),
            "exit never resurrects");
        require(
            !timeline.retarget(state.getScreenRevision(), state.getLayers(), state.getTransition(), 9000000000L, true),
            "duplicate frame never replays");
        timeline.retarget(state.getScreenRevision() + 1, state.getLayers(), state.getTransition(), 10000000000L, true);
        require(
            Math.abs(
                timeline.sample(11500000000L)
                    .get(0).alpha - .5)
                < 1e-9,
            "same picture replays in a new screen");
        ScreenTransition resume = new ScreenTransition();
        resume.retarget(state.getScreenRevision(), state.getLayers(), state.getTransition(), 0, false);
        require(
            resume.sample(0)
                .isEmpty(),
            "restored presentation applies final visibility without replay");
        timeline.prepare(state.getLayers(), state.getTransition(), 11500000000L);
        require(
            timeline.sample(12000000000L)
                .isEmpty(),
            "early advance cancels old sequence immediately");
        CanonicalGraphResource disabled = choice();
        List<CanonicalGraphNode> disabledNodes = new ArrayList<CanonicalGraphNode>();
        for (CanonicalGraphNode n : disabled.getGraph()
            .getNodes()) {
            Map<String, JsonElement> properties = n.getProperties();
            if ("choice".equals(n.getType())) for (JsonElement option : properties.get("options")
                .getAsJsonArray())
                option.getAsJsonObject()
                    .addProperty("condition_enabled", false);
            disabledNodes
                .add(new CanonicalGraphNode(n.getId(), n.getType(), n.getDisplayName(), n.getPorts(), properties));
        }
        disabled = new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            "disabled",
            "Disabled",
            new CanonicalGraph(
                disabledNodes,
                disabled.getGraph()
                    .getConnections()));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(disabled);
        require(
            runtime.getCurrentStep()
                .getOptions()
                .get(0)
                .isEnabled()
                && runtime.getCurrentStep()
                    .getOptions()
                    .get(0)
                    .isVisible(),
            "disabled prerequisite ignores retained false wire");
        System.out.println("ORDERED_CURRENT_SCREEN_ANIMATION_AND_CONDITION_SWITCH=PASS");
    }

    private static void independentImageEffects() {
        String stable = imageLayer(IMAGE, "stable", 2, 2), old = imageLayer(OTHER, "old", 1, 2),
            added = imageLayer(OTHER, "new", 1, 1);
        CanonicalSessionPresentation before = CanonicalSessionPresentation.EMPTY
            .apply(node("before", "screen", "{\"layers\":[" + stable + "," + old + "]}"));
        CanonicalSessionPresentation after = before
            .apply(node("after", "screen", "{\"layers\":[" + stable + "," + added + "]}"));
        CanonicalSessionPresentation restored = CanonicalSessionPresentation.fromJson(after.toJson());
        require(
            restored.getLayers()
                .get(1).enter.duration == 1
                && restored.getLayers()
                    .get(0).exit.duration == 2,
            "image effect roundtrip");
        ScreenTransition motion = new ScreenTransition();
        motion.retarget(before.getScreenRevision(), before.getLayers(), before.getTransition(), 0, false);
        motion.retarget(after.getScreenRevision(), after.getLayers(), after.getTransition(), 0, true);
        List<ScreenTransition.Sprite> mid = motion.sample(500000000L);
        for (ScreenTransition.Sprite sprite : mid) {
            double expected = "stable".equals(sprite.key) ? .15625 : .5;
            require(Math.abs(sprite.alpha - expected) < 1e-9, "new screen replays its own independent sequences");
        }
        require(
            mid.size() == 2 && motion.sample(4500000000L)
                .isEmpty() && !motion.active(4500000000L),
            "sequence exit remains hidden after completion");
        System.out.println("INDEPENDENT_IMAGE_ENTER_EXIT_ROUNDTRIP=PASS");
        JsonArray largest = new JsonArray();
        for (int i = 0; i < 32; i++) {
            JsonObject layer = new JsonParser()
                .parse(imageLayer(IMAGE, i + repeat('k', 94), 59.123456789012345, 59.123456789012345))
                .getAsJsonObject();
            for (String field : Arrays.asList("x", "y", "width", "height")) layer.addProperty(field, 2.123456789012345);
            for (String field : Arrays.asList("anchor_x", "anchor_y")) layer.addProperty(field, .1234567890123456);
            for (String field : Arrays.asList("enter", "exit")) {
                layer.getAsJsonObject(field)
                    .addProperty("type", "random_lines");
                layer.getAsJsonObject(field)
                    .addProperty("direction", "horizontal");
            }
            largest.add(layer);
        }
        CanonicalSessionPresentation large = CanonicalSessionPresentation.EMPTY
            .apply(node("large", "screen", "{\"layers\":" + largest + "}"));
        require(
            large.toJson()
                .length() > 16384
                && large.toJson()
                    .length() < CanonicalSessionPresentation.MAX_JSON_CHARS,
            "32 fully configured images fit bounded presentation budget");
        require(
            CanonicalSessionPresentation.fromJson(large.toJson())
                .getLayers()
                .size() == 32,
            "largest effect snapshot roundtrip");
        System.out.println("IMAGE_EFFECT_PRESENTATION_BUDGET=PASS");
    }

    private static String imageLayer(String media, String key, double enter, double exit) {
        return "{\"media_ref\":\"" + media
            + "\",\"morph_key\":\""
            + key
            + "\",\"x\":0,\"y\":0,\"width\":0.5,\"height\":0.5,\"anchor_x\":0,\"anchor_y\":0,\"z\":0,\"enter\":{\"type\":\"fade\",\"direction\":\"left\",\"duration\":"
            + enter
            + "},\"exit\":{\"type\":\"fade\",\"direction\":\"left\",\"duration\":"
            + exit
            + "}}";
    }

    private static CanonicalGraphNode screen(String id, String media, String key, double x, double w) {
        return node(
            id,
            "screen",
            "{\"layers\":[{\"morph_key\":\"" + key
                + "\",\"media_ref\":\""
                + media
                + "\",\"x\":"
                + x
                + ",\"y\":0,\"width\":"
                + w
                + ",\"height\":1,\"anchor_x\":0,\"anchor_y\":0,\"z\":0}],\"transition\":{\"type\":\"morph\",\"direction\":\"left\",\"duration\":1}}");
    }

    private static CanonicalGraphNode node(String id, String type, String json, CanonicalGraphPort... ports) {
        Map<String, JsonElement> props = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> field : new JsonParser().parse(json)
            .getAsJsonObject()
            .entrySet()) props.put(field.getKey(), field.getValue());
        return new CanonicalGraphNode(id, type, id, Arrays.asList(ports), props);
    }

    private static String repeat(char c, int n) {
        char[] chars = new char[n];
        Arrays.fill(chars, c);
        return new String(chars);
    }

    private static void require(boolean value, String reason) {
        if (!value) throw new AssertionError(reason);
    }

    private static void reject(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected rejection");
        } catch (IllegalArgumentException expected) {} catch (SessionTextLimit.ProjectionFailure expected) {}
    }
}
