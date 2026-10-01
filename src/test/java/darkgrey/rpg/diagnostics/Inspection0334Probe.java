package darkgrey.rpg.diagnostics;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.runtime.DynamicContentText;
import io.netty.buffer.Unpooled;

public final class Inspection0334Probe {

    public static void main(String[] args) throws Exception {
        readableSummary();
        java.io.Reader vectorFile = new java.io.InputStreamReader(
            new java.io.FileInputStream("src/test/resources/dynamic-content-0334.json"),
            "UTF-8");
        com.google.gson.JsonArray vectors = new com.google.gson.JsonParser().parse(vectorFile)
            .getAsJsonArray();
        vectorFile.close();
        for (com.google.gson.JsonElement element : vectors) {
            com.google.gson.JsonObject vector = element.getAsJsonObject();
            boolean rejected = false;
            String actual = null;
            try {
                actual = DynamicContentText.resolve(
                    vector.get("input")
                        .getAsString(),
                    new DynamicContentText.Resolver() {

                        public String resolve(String type, String item) {
                            return "player_name".equals(type) ? "Alice" : "player_level".equals(type) ? "12" : "3";
                        }
                    });
            } catch (RuntimeException invalid) {
                rejected = true;
            }
            if (vector.has("invalid") ? !rejected
                : rejected || !vector.get("expected")
                    .getAsString()
                    .equals(actual))
                throw new AssertionError("Shared vector: " + vector);
        }
        UUID player = UUID.randomUUID();
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", 1);
        NBTTagList records = new NBTTagList();
        for (int i = 0; i < 3; i++) {
            NBTTagCompound row = new NBTTagCompound();
            row.setString("player_uuid", (i == 2 ? UUID.randomUUID() : player).toString());
            row.setString("task_resource_id", "demo:task");
            row.setString("task_node_placement_id", "placement-" + i);
            row.setString("status", i == 0 ? "ERROR" : "ACTIVE");
            records.appendTag(row);
        }
        root.setTag("instances", records);
        NBTTagCompound before = (NBTTagCompound) root.copy();
        List<NBTTagCompound> rows = new ArrayList<NBTTagCompound>();
        PlayerStateInspection.collect(rows, "Task", root, player, "测试快照", ProjectSnapshot.empty());
        if (rows.size() != 2 || !root.equals(before)) throw new AssertionError("Player isolation or mutation");
        if (rows.get(0)
            .toString()
            .contains(player.toString())) throw new AssertionError("Player UUID leaked");
        if (!rows.get(0)
            .toString()
            .contains("未通过结构校验")) throw new AssertionError("Malformed record hidden");
        if (!rows.get(0)
            .toString()
            .contains("所属 Story 资源缺失")) throw new AssertionError("Missing owner hidden");
        PlayerStatePacket packet = new PlayerStatePacket(2, 42, rows.get(0));
        io.netty.buffer.ByteBuf buffer = Unpooled.buffer();
        packet.toBytes(buffer);
        PlayerStatePacket decoded = new PlayerStatePacket();
        decoded.fromBytes(buffer);
        if (decoded.request != 42 || !decoded.data.equals(packet.data)) throw new AssertionError("Packet roundtrip");
        String source = DynamicContentText.PREFIX
            + "[\"你好\",{\"type\":\"player_name\"},{\"type\":\"item_count\",\"item_id\":\"demo:iron\"}]";
        String a = DynamicContentText.resolve(source, new DynamicContentText.Resolver() {

            public String resolve(String type, String item) {
                return "player_name".equals(type) ? "Alice" : "3";
            }
        });
        String b = DynamicContentText.resolve(source, new DynamicContentText.Resolver() {

            public String resolve(String type, String item) {
                return "player_name".equals(type) ? "Bob" : "0";
            }
        });
        if (!"你好Alice3".equals(a) || !"你好Bob0".equals(b)) throw new AssertionError("Dynamic player isolation");
        if (!"{{player_name}}".equals(DynamicContentText.resolve("{{player_name}}", null)))
            throw new AssertionError("Old prose changed");
        if ("1".equals(System.getenv("DGR_INSPECTION_SCALE"))) {
            NBTTagCompound large = new NBTTagCompound();
            NBTTagList many = new NBTTagList();
            for (int i = 0; i < 10000; i++) {
                NBTTagCompound row = (NBTTagCompound) records.getCompoundTagAt(0)
                    .copy();
                row.setString("player_uuid", (i < 1000 ? player : new UUID(0, i + 1)).toString());
                row.setString("task_node_placement_id", "scale-" + i);
                many.appendTag(row);
            }
            large.setTag("instances", many);
            NBTTagCompound unchanged = (NBTTagCompound) large.copy();
            double[] samples = new double[30];
            for (int i = 0; i < 35; i++) {
                long started = System.nanoTime();
                List<NBTTagCompound> projected = new ArrayList<NBTTagCompound>();
                PlayerStateInspection.collect(projected, "Task", large, player, "scale", ProjectSnapshot.empty());
                double elapsed = (System.nanoTime() - started) / 1000000.0;
                if (projected.size() != 1000) throw new AssertionError("Scale projection lost records");
                if (i >= 5) samples[i - 5] = elapsed;
            }
            if (!large.equals(unchanged)) throw new AssertionError("Scale query mutated source");
            java.util.Arrays.sort(samples);
            System.out.println(
                "INSPECTION_SCALE records=10000 target=1000 other_players=9000 n=30 median_ms=" + samples[15]
                    + " p95_ms="
                    + samples[28]
                    + " max_ms="
                    + samples[29]
                    + " used_heap="
                    + (Runtime.getRuntime()
                        .totalMemory()
                        - Runtime.getRuntime()
                            .freeMemory())
                    + " excludes disk decompression, network and GUI");
        }
        System.out.println(
            "Inspection0334Probe PASS: detached read, malformed record retention, placement separation, player isolation, bounded packet roundtrip, dynamic text");
    }

    private static void readableSummary() {
        java.util.Map<String, com.google.gson.JsonElement> properties = new java.util.LinkedHashMap<String, com.google.gson.JsonElement>();
        com.google.gson.JsonParser parser = new com.google.gson.JsonParser();
        properties.put("repeat_policy", parser.parse("\"repeatable\""));
        properties.put("repeat_condition", parser.parse("{\"type\":\"cooldown\",\"value\":1,\"unit\":\"hours\"}"));
        properties.put(
            "triggers",
            parser.parse(
                "[{\"port_id\":\"entry\",\"display_name\":\"进入故事\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0}]"));
        darkgrey.rpg.graph.canonical.CanonicalGraphPort port = new darkgrey.rpg.graph.canonical.CanonicalGraphPort(
            "entry",
            "进入故事",
            darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection.OUTPUT,
            darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW,
            0);
        darkgrey.rpg.graph.canonical.CanonicalGraphNode start = new darkgrey.rpg.graph.canonical.CanonicalGraphNode(
            "start_raw_id",
            "start",
            "开始",
            java.util.Collections.singletonList(port),
            properties);
        darkgrey.rpg.graph.canonical.CanonicalGraphResource story = new darkgrey.rpg.graph.canonical.CanonicalGraphResource(
            1,
            darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind.STORY,
            "raw_story_id",
            "酒馆聊天",
            new darkgrey.rpg.graph.canonical.CanonicalGraph(
                java.util.Collections.singletonList(start),
                java.util.Collections.<darkgrey.rpg.graph.canonical.CanonicalGraphConnection>emptyList()));
        NBTTagCompound record = new NBTTagCompound(), runtime = new NBTTagCompound();
        runtime.setString("status", "TERMINATED");
        runtime.setString("repeat_policy", "repeatable");
        runtime.setString("wait_kind", "NONE");
        runtime.setString("current_node_id", "start_raw_id");
        record.setTag("runtime", runtime);
        record.setLong("terminal_time", 100000L);
        java.time.Clock late = java.time.Clock
            .fixed(java.time.Instant.ofEpochMilli(4000000L), java.time.ZoneOffset.UTC);
        NBTTagCompound before = (NBTTagCompound) record.copy();
        String summary = PlayerStateSummary
            .build(
                "Story",
                record,
                story,
                ProjectSnapshot.empty(),
                "服务器当前状态",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!summary.contains("故事已正常结束") || !summary.contains("已可再次启动")
            || summary.contains("NONE")
            || summary.contains("raw_id")
            || summary.contains("可再次启动时间")
            || !before.equals(record)) throw new AssertionError("Readable terminal summary: " + summary);
        String waiting = PlayerStateSummary
            .build(
                "Story",
                record,
                story,
                ProjectSnapshot.empty(),
                "存档",
                java.util.Collections.<String>emptyList(),
                java.time.Clock.fixed(java.time.Instant.ofEpochMilli(200000L), java.time.ZoneOffset.UTC))
            .toString();
        if (!waiting.contains("尚在冷却") || !waiting.contains("可再次启动时间"))
            throw new AssertionError("Cooldown summary: " + waiting);
        runtime.setString("status", "ACTIVE");
        runtime.setString("wait_kind", "CONDITION");
        String active = PlayerStateSummary
            .build(
                "Story",
                record,
                story,
                ProjectSnapshot.empty(),
                "存档",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!active.contains("等待节点条件满足") || !active.contains("当前步骤：开始")) throw new AssertionError(active);
        runtime.setString("wait_kind", "SESSION");
        runtime.setString("wait_resource_id", "missing_session_id");
        String missingWait = PlayerStateSummary
            .build(
                "Story",
                record,
                story,
                ProjectSnapshot.empty(),
                "存档",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!missingWait.contains("等待的会话或任务资源缺失") || missingWait.contains("missing_session_id"))
            throw new AssertionError(missingWait);
        runtime.setString("status", "ERROR");
        String error = PlayerStateSummary
            .build(
                "Story",
                record,
                null,
                ProjectSnapshot.empty(),
                "存档",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!error.contains("资源已缺失") || !error.contains("没有保存具体原因")) throw new AssertionError(error);
        runtime.setString("status", "TERMINATED");
        runtime.setString("repeat_policy", "once");
        String once = PlayerStateSummary
            .build(
                "Story",
                record,
                story,
                ProjectSnapshot.empty(),
                "存档",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!once.contains("不允许重复")) throw new AssertionError(once);
        java.util.Map<String, com.google.gson.JsonElement> goalProperties = new java.util.LinkedHashMap<String, com.google.gson.JsonElement>();
        goalProperties.put("description", parser.parse("\"击败三只史莱姆\""));
        goalProperties.put("required", parser.parse("3"));
        darkgrey.rpg.graph.canonical.CanonicalGraphNode goal = new darkgrey.rpg.graph.canonical.CanonicalGraphNode(
            "raw_goal_id",
            "objective",
            "目标",
            java.util.Collections.<darkgrey.rpg.graph.canonical.CanonicalGraphPort>emptyList(),
            goalProperties);
        darkgrey.rpg.graph.canonical.CanonicalGraphResource task = new darkgrey.rpg.graph.canonical.CanonicalGraphResource(
            1,
            darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind.TASK,
            "raw_task_id",
            "清理史莱姆",
            new darkgrey.rpg.graph.canonical.CanonicalGraph(
                java.util.Collections.singletonList(goal),
                java.util.Collections.<darkgrey.rpg.graph.canonical.CanonicalGraphConnection>emptyList()));
        NBTTagCompound taskRecord = new NBTTagCompound(), objective = new NBTTagCompound(),
            progress = new NBTTagCompound();
        taskRecord.setString("status", "ACTIVE");
        objective.setString("key", "raw_goal_id");
        objective.setString("value", "ACTIVE");
        progress.setString("key", "raw_goal_id");
        progress.setInteger("value", 1);
        NBTTagList objectives = new NBTTagList(), counts = new NBTTagList();
        objectives.appendTag(objective);
        counts.appendTag(progress);
        taskRecord.setTag("objective_statuses", objectives);
        taskRecord.setTag("progress", counts);
        NBTTagCompound taskBefore = (NBTTagCompound) taskRecord.copy();
        String taskSummary = PlayerStateSummary
            .build(
                "Task",
                taskRecord,
                task,
                ProjectSnapshot.empty(),
                "离线存档",
                java.util.Collections.<String>emptyList(),
                late)
            .toString();
        if (!taskSummary.contains("击败三只史莱姆 · 进行中 · 1 / 3") || !taskSummary.contains("清理史莱姆")
            || taskSummary.contains("raw_goal_id")
            || !taskBefore.equals(taskRecord)) throw new AssertionError(taskSummary);
        System.out.println("READABLE_OP_SUMMARY=PASS");
    }
}
