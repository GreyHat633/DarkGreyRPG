package darkgrey.rpg.task.forge;

import java.lang.reflect.Field;
import java.nio.charset.StandardCharsets;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.entity.projectile.EntityArrow;
import net.minecraft.util.DamageSource;
import net.minecraft.util.EntityDamageSource;
import net.minecraft.util.EntityDamageSourceIndirect;
import net.minecraftforge.common.util.FakePlayer;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoader;
import sun.misc.Unsafe;

/** P2 Task metadata parity and actual 1.7.10 lethal damage-source contracts. */
public final class Task0330Probe {

    public static void main(String[] args) throws Exception {
        String base = "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"task\",\"display_name\":\"Task\",\"graph\":{\"nodes\":[{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[],\"properties\":{}}],\"connections\":[]}";
        CanonicalGraphResourceLoader loader = new CanonicalGraphResourceLoader();
        String json = base + ",\"task_metadata\":{\"description\":\"背景说明\\n第二段\"}}";
        CanonicalGraphResource resource = loader
            .load(json.getBytes(StandardCharsets.UTF_8), "task.json", CanonicalGraphResourceKind.TASK);
        require(
            "背景说明\n第二段".equals(
                resource.getTaskMetadata()
                    .getDescription()),
            "Task description roundtrip");
        require(
            loader.load((base + "}").getBytes(StandardCharsets.UTF_8), "task.json", CanonicalGraphResourceKind.TASK)
                .getTaskMetadata() == null,
            "Old Task default");
        for (String bad : new String[] { json.replace("\"task\",\"id\"", "\"story\",\"id\""),
            base + ",\"task_metadata\":null}", base + ",\"task_metadata\":{\"description\":3}}",
            base + ",\"task_metadata\":{\"description\":\"x\",\"extra\":true}}" }) {
            try {
                loader.load(bad.getBytes(StandardCharsets.UTF_8), "task.json", null);
                throw new AssertionError("Invalid Task metadata accepted");
            } catch (CanonicalGraphResourceException expected) {}
        }
        Field field = Unsafe.class.getDeclaredField("theUnsafe");
        field.setAccessible(true);
        Unsafe unsafe = (Unsafe) field.get(null);
        EntityPlayerMP player = (EntityPlayerMP) unsafe.allocateInstance(EntityPlayerMP.class);
        FakePlayer fake = (FakePlayer) unsafe.allocateInstance(FakePlayer.class);
        EntityArrow arrow = (EntityArrow) unsafe.allocateInstance(EntityArrow.class);
        require(
            CanonicalTaskForgeEventNormalizer.creditedKiller(DamageSource.causePlayerDamage(player)) == player,
            "Real melee");
        require(
            CanonicalTaskForgeEventNormalizer.creditedKiller(DamageSource.causeArrowDamage(arrow, player)) == player,
            "Owned projectile");
        for (DamageSource source : new DamageSource[] { DamageSource.causePlayerDamage(fake),
            DamageSource.causeArrowDamage(arrow, fake), DamageSource.causeArrowDamage(arrow, null), DamageSource.fall,
            DamageSource.inFire, new EntityDamageSource("thorns", player),
            new EntityDamageSourceIndirect("magic", arrow, player), null })
            require(CanonicalTaskForgeEventNormalizer.creditedKiller(source) == null, "Non-player lethal hit credited");
        objectiveVectors();
        submissionVectors();
        packetVectors();
        System.out.println("TASK_0330_METADATA_SCOPE_AND_ROUNDTRIP=PASS");
        System.out.println("TASK_0330_KILL_MELEE_PROJECTILE_FAKEPLAYER_ENVIRONMENT=PASS");
    }

    private static CanonicalGraphResource task(String type, String extra) {
        com.google.gson.JsonObject props = new com.google.gson.JsonParser()
            .parse("{\"objective_type\":\"" + type + "\",\"description\":\"目标\"" + extra + "}")
            .getAsJsonObject();
        java.util.Map<String, com.google.gson.JsonElement> values = new java.util.LinkedHashMap<String, com.google.gson.JsonElement>();
        for (java.util.Map.Entry<String, com.google.gson.JsonElement> e : props.entrySet())
            values.put(e.getKey(), e.getValue());
        CanonicalGraphNode objective = new CanonicalGraphNode(
            "objective",
            "objective",
            "目标",
            java.util.Collections.singletonList(
                new CanonicalGraphPort(
                    "logic_status",
                    "完成",
                    CanonicalGraphPortDirection.OUTPUT,
                    CanonicalGraphInterfaceKind.LOGIC,
                    0)),
            values);
        CanonicalGraphNode settle = new CanonicalGraphNode(
            "settle",
            "settle",
            "结算",
            java.util.Collections.singletonList(
                new CanonicalGraphPort(
                    "done",
                    "完成",
                    CanonicalGraphPortDirection.INPUT,
                    CanonicalGraphInterfaceKind.LOGIC,
                    0)),
            java.util.Collections.<String, com.google.gson.JsonElement>emptyMap());
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "task",
            "任务",
            new CanonicalGraph(
                java.util.Arrays.asList(objective, settle),
                java.util.Collections.singletonList(
                    new CanonicalGraphConnection(
                        "objective",
                        "logic_status",
                        "settle",
                        "done",
                        CanonicalGraphInterfaceKind.LOGIC))));
    }

    private static darkgrey.rpg.task.runtime.CanonicalTaskEvent position(int dimension, double x, double y, double z) {
        java.util.Map<String, String> values = new java.util.LinkedHashMap<String, String>();
        values.put("dimension_id", String.valueOf(dimension));
        values.put("x", String.valueOf(x));
        values.put("y", String.valueOf(y));
        values.put("z", String.valueOf(z));
        return new darkgrey.rpg.task.runtime.CanonicalTaskEvent("reach_region", values, 1);
    }

    private static void objectiveVectors() {
        CanonicalGraphResource collect = task(
            "collect_item",
            ",\"required\":5,\"item\":\"minecraft:apple\",\"metadata\":{}");
        darkgrey.rpg.task.runtime.CanonicalTaskRuntime runtime = darkgrey.rpg.task.runtime.CanonicalTaskRuntime
            .start(collect);
        for (int count : new int[] { 3, 2, 0, 5 }) {
            runtime.accept(darkgrey.rpg.task.runtime.CanonicalTaskEvent.collectItem("minecraft:apple", count));
            require(
                runtime.getProgress()
                    .get("objective") == count,
                "Collect must equal current inventory, never lifetime total");
        }
        runtime = darkgrey.rpg.task.runtime.CanonicalTaskRuntime.restore(collect, runtime.snapshot());
        require(
            !runtime.accept(darkgrey.rpg.task.runtime.CanonicalTaskEvent.collectItem("minecraft:apple", 0)),
            "Completed collect must latch after restore");
        CanonicalGraphResource region = task(
            "reach_region",
            ",\"dimension_id\":7,\"center_x\":1.5,\"center_y\":2.5,\"center_z\":3.5,\"radius\":2");
        for (double[] point : new double[][] { { -2, 0, 0 }, { 2, 0, 0 }, { 0, -2, 0 }, { 0, 2, 0 }, { 0, 0, -2 },
            { 0, 0, 2 }, { -2, -2, -2 }, { -2, -2, 2 }, { -2, 2, -2 }, { -2, 2, 2 }, { 2, -2, -2 }, { 2, -2, 2 },
            { 2, 2, -2 }, { 2, 2, 2 } }) {
            runtime = darkgrey.rpg.task.runtime.CanonicalTaskRuntime.start(region);
            require(!runtime.accept(position(0, 1.5 + point[0], 2.5 + point[1], 3.5 + point[2])), "Dimension mismatch");
            require(
                runtime.accept(position(7, 1.5 + point[0], 2.5 + point[1], 3.5 + point[2])),
                "Cube inclusive face/corner");
        }
        runtime = darkgrey.rpg.task.runtime.CanonicalTaskRuntime.start(region);
        require(!runtime.accept(position(7, 3.500001, 2.5, 3.5)), "Outside cube accepted");
        CanonicalGraphResource point = task(
            "reach_region",
            ",\"dimension_id\":7,\"center_x\":1.5,\"center_y\":2.5,\"center_z\":3.5,\"radius\":0");
        runtime = darkgrey.rpg.task.runtime.CanonicalTaskRuntime.start(point);
        require(!runtime.accept(position(7, 1.500001, 2.5, 3.5)), "Radius zero is a point");
        require(runtime.accept(position(7, 1.5, 2.5, 3.5)), "Radius zero center");
        System.out.println("TASK_0330_COLLECT_CURRENT_COUNT_AND_LATCH=PASS");
        System.out.println("TASK_0330_REGION_6_FACES_8_CORNERS_DIMENSION_AND_ZERO=PASS");
    }

    private static void submissionVectors() throws Exception {
        final net.minecraft.item.Item apple = new net.minecraft.item.Item();
        java.lang.reflect.Method register = net.minecraft.item.Item.itemRegistry.getClass()
            .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
        register.setAccessible(true);
        register.invoke(net.minecraft.item.Item.itemRegistry, 31001, "minecraft:apple", apple);
        final CanonicalGraphResource resource = task(
            "submit_item",
            ",\"required\":5,\"item\":\"minecraft:apple\",\"metadata\":{}");
        final CanonicalGraphNode objective = resource.getGraph()
            .getNodes()
            .get(0);
        final darkgrey.rpg.item.identity.ItemIdentitySavedData identities = new darkgrey.rpg.item.identity.ItemIdentitySavedData();
        net.minecraft.item.ItemStack[] insufficient = { new net.minecraft.item.ItemStack(apple, 2),
            new net.minecraft.item.ItemStack(apple, 2) };
        require(
            !CanonicalTaskInventory.removeExact(insufficient, objective, identities, 5)
                && insufficient[0].stackSize == 2
                && insufficient[1].stackSize == 2,
            "Insufficient submission must remove nothing");
        final net.minecraft.item.ItemStack[] inventory = { new net.minecraft.item.ItemStack(apple, 3),
            new net.minecraft.item.ItemStack(apple, 4) };
        darkgrey.rpg.task.persistence.CanonicalTaskSavedData data = new darkgrey.rpg.task.persistence.CanonicalTaskSavedData();
        data.bind(new darkgrey.rpg.task.instance.CanonicalTaskResourceResolver() {

            public CanonicalGraphResource resolve(String id) {
                return resource;
            }
        });
        java.util.UUID player = java.util.UUID.fromString("11111111-1111-1111-1111-111111111111");
        data.start(player, "story", "placement", resource, 100);
        final net.minecraft.item.ItemStack[] before = CanonicalTaskInventory.copy(inventory);
        darkgrey.rpg.task.runtime.CanonicalTaskEvent event = new darkgrey.rpg.task.runtime.CanonicalTaskEvent(
            "submit_item",
            java.util.Collections.singletonMap("item", "minecraft:apple"),
            5);
        net.minecraft.nbt.NBTTagCompound saved = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(saved);
        try {
            data.sampleObjective(
                player,
                "story",
                "placement",
                "objective",
                event,
                101,
                new darkgrey.rpg.task.persistence.CanonicalTaskSavedData.ObjectiveCommit() {

                    public void commit() {
                        inventory[0] = null;
                        throw new IllegalStateException("Injected inventory failure");
                    }

                    public void rollback() {
                        for (int i = 0; i < inventory.length; i++) inventory[i] = before[i].copy();
                    }
                });
            throw new AssertionError("Commit failure accepted");
        } catch (IllegalStateException expected) {}
        net.minecraft.nbt.NBTTagCompound after = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(after);
        require(
            saved.equals(after) && CanonicalTaskInventory.count(inventory, objective, identities) == 7,
            "Failed commit did not roll back inventory and Task");
        final int[] calls = { 0 };
        darkgrey.rpg.task.persistence.CanonicalTaskSavedData.ObjectiveCommit effect = new darkgrey.rpg.task.persistence.CanonicalTaskSavedData.ObjectiveCommit() {

            public void commit() {
                calls[0]++;
                require(CanonicalTaskInventory.removeExact(inventory, objective, identities, 5), "Exact removal");
            }

            public void rollback() {
                for (int i = 0; i < inventory.length; i++) inventory[i] = before[i].copy();
            }
        };
        require(
            data.sampleObjective(player, "story", "placement", "objective", event, 102, effect) != null,
            "Submission did not complete");
        require(
            data.sampleObjective(player, "story", "placement", "objective", event, 103, effect) == null
                && calls[0] == 1,
            "Repeated submission consumed twice");
        require(CanonicalTaskInventory.count(inventory, objective, identities) == 2, "Cross-slot exact deduction");
        net.minecraft.nbt.NBTTagCompound committed = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(committed);
        darkgrey.rpg.task.persistence.CanonicalTaskSavedData restarted = new darkgrey.rpg.task.persistence.CanonicalTaskSavedData();
        restarted.readFromNBT(committed);
        restarted.bind(new darkgrey.rpg.task.instance.CanonicalTaskResourceResolver() {

            public CanonicalGraphResource resolve(String id) {
                return resource;
            }
        });
        require(
            restarted.sampleObjective(player, "story", "placement", "objective", event, 104, effect) == null
                && calls[0] == 1,
            "Restored submission replay consumed twice");
        System.out.println("TASK_0330_SUBMIT_EXACT_INSUFFICIENT_ROLLBACK_AND_REPLAY=PASS");
    }

    private static void packetVectors() {
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmit packet = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmit(
            1,
            "task",
            "objective");
        io.netty.buffer.ByteBuf bytes = io.netty.buffer.Unpooled.buffer();
        packet.toBytes(bytes);
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmit decoded = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmit();
        decoded.fromBytes(bytes.copy());
        io.netty.buffer.ByteBuf encoded = io.netty.buffer.Unpooled.buffer();
        decoded.toBytes(encoded);
        require(bytes.equals(encoded), "Submit codec roundtrip");
        for (int size = 0; size < bytes.readableBytes(); size++) {
            try {
                new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmit().fromBytes(bytes.copy(0, size));
                throw new AssertionError("Truncated submit accepted");
            } catch (IllegalArgumentException expected) {}
        }
        System.out.println("TASK_0330_SUBMIT_CODEC_TRUNCATION=PASS");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
