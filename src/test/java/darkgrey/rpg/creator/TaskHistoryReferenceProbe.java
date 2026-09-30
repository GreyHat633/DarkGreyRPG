package darkgrey.rpg.creator;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import com.google.gson.JsonElement;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.client.gui.TaskObjectiveText;
import darkgrey.rpg.graph.canonical.*;

public final class TaskHistoryReferenceProbe {

    public static void main(String[] args) throws Exception {
        NBTTagCompound old = new NBTTagCompound();
        String player = "82c60805-44a1-4345-bdaa-30bb821ff126";
        old.setString("player", player);
        old.setString(
            "id",
            "history:" + part(player) + part("story") + part("placement") + part("GreyHat_:KillSlimes"));
        old.setString("title", "旧标题");
        old.setLong("completion_count", 2);
        old.setTag("objectives", new NBTTagList());
        NBTTagCompound original = (NBTTagCompound) old.copy();
        CanonicalGraphResource task = task("新标题", 3);
        NBTTagCompound projected = CanonicalTaskHistoryProjection
            .project(old, id -> "GreyHat_:KillSlimes".equals(id) ? task : null, text -> text, null);
        require(old.equals(original), "read-only history");
        io.netty.buffer.ByteBuf wire = io.netty.buffer.Unpooled.buffer();
        new CreatorSnapshot(1, projected).toBytes(wire);
        CreatorSnapshot decoded = new CreatorSnapshot();
        decoded.fromBytes(wire);
        require(
            decoded.data.equals(projected) && !wire.isReadable(),
            "optional reference fields round trip within existing wire boundary");
        wire.release();
        require(
            projected.getLong("completion_count") == 2 && projected.getString("title")
                .equals("旧标题"),
            "true count and historic title");
        require(
            projected.getString("content_source")
                .equals("current_definition"),
            "reference source");
        NBTTagList goals = projected.getTagList("reference_objectives", 10);
        require(
            goals.tagCount() == 2 && projected.getTagList("objectives", 10)
                .tagCount() == 0,
            "two references separate from history");
        for (int i = 0; i < goals.tagCount(); i++) require(
            !goals.getCompoundTagAt(i)
                .hasKey("current")
                && !goals.getCompoundTagAt(i)
                    .hasKey("status"),
            "no invented completion");
        require(
            TaskObjectiveText.referenceLines(goals.getCompoundTagAt(0))
                .get(0)
                .contains("消灭史莱姆，要求 3 只"),
            "readable kill requirement");
        require(
            TaskObjectiveText.referenceLines(goals.getCompoundTagAt(1))
                .get(0)
                .contains("与酒馆老板对话"),
            "readable interaction");
        NBTTagCompound changed = CanonicalTaskHistoryProjection.project(old, id -> task("再改名", 8), text -> text, null);
        require(
            changed.getTagList("reference_objectives", 10)
                .getCompoundTagAt(0)
                .getInteger("required") == 8 && old.equals(original),
            "current definition changes without rewriting history");
        require(
            CanonicalTaskHistoryProjection.project(old, id -> null, text -> text, null)
                .getString("content_source")
                .equals("missing"),
            "missing resource");
        old.setString("id", "history:999:broken");
        require(
            CanonicalTaskHistoryProjection.resourceId(old)
                .isEmpty(),
            "malformed legacy id rejected");
        old.setString("task_resource_id", "GreyHat_:KillSlimes");
        require(
            CanonicalTaskHistoryProjection.resourceId(old)
                .equals("GreyHat_:KillSlimes"),
            "explicit stable id");
        NBTTagList snapshot = new NBTTagList();
        NBTTagCompound goal = new NBTTagCompound();
        goal.setString("text", "真实历史目标");
        goal.setInteger("current", 3);
        snapshot.appendTag(goal);
        old.setTag("objectives", snapshot);
        NBTTagCompound actual = CanonicalTaskHistoryProjection.project(
            old,
            id -> { throw new AssertionError("snapshot must not query current definition"); },
            text -> text,
            null);
        require(
            actual.getString("content_source")
                .equals("snapshot") && !actual.hasKey("reference_objectives"),
            "snapshot precedence");
        if (args.length > 0) verifyActualCopy(java.nio.file.Paths.get(args[0]));
        System.out.println("TASK_HISTORY_REFERENCE_PROBE=PASS");
    }

    private static void verifyActualCopy(java.nio.file.Path root) throws Exception {
        java.nio.file.Path historyFile = root.resolve("history-original.dat");
        byte[] before = java.nio.file.Files.readAllBytes(historyFile);
        NBTTagCompound persisted;
        try (java.io.InputStream input = java.nio.file.Files.newInputStream(historyFile)) {
            persisted = net.minecraft.nbt.CompressedStreamTools.readCompressed(input)
                .getCompoundTag("data");
        }
        NBTTagCompound old = persisted.getTagList("summaries", 10)
            .getCompoundTagAt(0);
        require(
            old.getLong("completion_count") == 2 && old.getTagList("objectives", 10)
                .tagCount() == 0,
            "actual old record has count two and no historical goals");
        NBTTagCompound emptyRuntime;
        try (java.io.InputStream input = java.nio.file.Files.newInputStream(root.resolve("tasks-original.dat"))) {
            emptyRuntime = net.minecraft.nbt.CompressedStreamTools.readCompressed(input)
                .getCompoundTag("data");
        }
        require(
            emptyRuntime.getTagList("instances", 10)
                .tagCount() == 0,
            "actual runtime cleared");
        CanonicalGraphResource resource;
        try (java.util.zip.ZipFile pack = new java.util.zip.ZipFile(
            root.resolve("LegacyReferenceCopy.dgrs")
                .toFile())) {
            String path = "resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json";
            try (java.io.InputStream input = pack.getInputStream(pack.getEntry(path))) {
                java.io.ByteArrayOutputStream data = new java.io.ByteArrayOutputStream();
                byte[] buffer = new byte[4096];
                int length;
                while ((length = input.read(buffer)) != -1) data.write(buffer, 0, length);
                resource = new CanonicalGraphResourceLoader()
                    .load(data.toByteArray(), path, CanonicalGraphResourceKind.TASK);
            }
        }
        NBTTagCompound saved = (NBTTagCompound) persisted.copy();
        NBTTagCompound reference = CanonicalTaskHistoryProjection.project(
            old,
            id -> id.equals(resource.getId()) ? resource : null,
            text -> text,
            new darkgrey.rpg.item.identity.ItemIdentitySavedData());
        NBTTagList goals = reference.getTagList("reference_objectives", 10);
        require(
            goals.tagCount() == 2 && goals.getCompoundTagAt(0)
                .getInteger("required") == 3,
            "actual stable resource resolves two author goals");
        require(
            TaskObjectiveText.referenceLines(goals.getCompoundTagAt(0))
                .get(0)
                .contains("消灭史莱姆")
                && TaskObjectiveText.referenceLines(goals.getCompoundTagAt(1))
                    .get(0)
                    .contains("与酒馆老板对话"),
            "actual readable goals");
        require(
            persisted.equals(saved) && java.util.Arrays.equals(before, java.nio.file.Files.readAllBytes(historyFile)),
            "actual old record remains byte-identical");
        System.out.println("ACTUAL_LEGACY_COPY_COUNT_TWO_REFERENCE_GOALS=PASS");
    }

    private static String part(String value) {
        return value.length() + ":" + value;
    }

    private static CanonicalGraphResource task(String title, int count) {
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "GreyHat_:KillSlimes",
            title,
            new CanonicalGraph(
                Arrays
                    .asList(goal("kill", "kill_entity", "消灭史莱姆", count), goal("talk", "interact_actor", "与酒馆老板对话", 1)),
                Collections.<CanonicalGraphConnection>emptyList()),
            new CanonicalTaskMetadata("任务简介"));
    }

    private static CanonicalGraphNode goal(String id, String type, String text, int count) {
        Map<String, JsonElement> fields = new LinkedHashMap<String, JsonElement>();
        fields.put("objective_type", new JsonPrimitive(type));
        fields.put("description", new JsonPrimitive(text));
        fields.put("required", new JsonPrimitive(count));
        return new CanonicalGraphNode(id, "objective", text, Collections.<CanonicalGraphPort>emptyList(), fields);
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
