package darkgrey.rpg.task.forge;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoader;
import darkgrey.rpg.task.instance.CanonicalTaskInstance;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskTransactionJournal;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Crash injection executes the same atomic journal used by real player transactions. */
public final class TaskReward0330Probe {

    public static void main(String[] args) throws Exception {
        runtime();
        journal(java.nio.file.Paths.get(args[0]));
        itemVectors();
        NBTTagCompound xp = new NBTTagCompound();
        for (int total : new int[] { 0, 1, 254, 255, 824, 825, 826, Integer.MAX_VALUE }) {
            CanonicalTaskPlayerTransactions.setXp(xp, total);
            require(
                xp.getInteger("total") == total && xp.getFloat("fraction") >= 0 && xp.getFloat("fraction") < 1,
                "XP range " + total);
        }
        CanonicalTaskPlayerTransactions.setXp(xp, 825);
        require(xp.getInteger("level") == 30 && xp.getFloat("fraction") == 0, "1.7.10 XP level boundary");
        System.out.println("TASK_REWARD_0330_XP_BOUNDARIES=PASS");
    }

    private static void runtime() {
        String json = "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"task\",\"display_name\":\"Task\",\"graph\":{\"nodes\":["
            + "{\"id\":\"in\",\"type\":\"logic_input\",\"display_name\":\"Input\",\"ports\":[{\"port_id\":\"logic_out\",\"display_name\":\"Output\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":0}],\"properties\":{\"port_id\":\"gate\",\"display_name\":\"Gate\"}},"
            + "{\"id\":\"reward\",\"type\":\"reward\",\"display_name\":\"Reward\",\"ports\":[{\"port_id\":\"logic_in\",\"display_name\":\"Input\",\"direction\":\"input\",\"kind\":\"logic\",\"order\":0}],\"properties\":{\"entries\":[{\"type\":\"xp\",\"amount\":250},{\"type\":\"item\",\"item\":\"apple\",\"amount\":-2}]}},"
            + "{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[{\"port_id\":\"done\",\"display_name\":\"Done\",\"direction\":\"input\",\"kind\":\"logic\",\"order\":0}],\"properties\":{}}],"
            + "\"connections\":[{\"from_node_id\":\"in\",\"from_port_id\":\"logic_out\",\"to_node_id\":\"reward\",\"to_port_id\":\"logic_in\",\"interface_kind\":\"logic\"}]}}";
        CanonicalGraphResource resource = new CanonicalGraphResourceLoader()
            .load(json.getBytes(StandardCharsets.UTF_8), "task.json", CanonicalGraphResourceKind.TASK);
        CanonicalTaskInstance task = CanonicalTaskInstance
            .start(UUID.randomUUID(), "story", "placement1", resource, 100);
        task.setLogicInput("gate", true, 101);
        require(
            Boolean.FALSE.equals(
                task.snapshot()
                    .getRuntimeSnapshot()
                    .getRewardStates()
                    .get("reward")),
            "Eligible pending");
        NBTTagCompound saved = CanonicalTaskInstanceNbtCodec
            .encode(java.util.Collections.singletonList(task.snapshot()));
        CanonicalTaskInstanceSnapshot restored = CanonicalTaskInstanceNbtCodec.decode(saved)
            .get(0);
        task = CanonicalTaskInstance.restore(restored, resource);
        require(task.grantReward("reward", 102), "First grant");
        task.setLogicInput("gate", false, 103);
        task.setLogicInput("gate", true, 104);
        require(!task.grantReward("reward", 105), "False true replay");
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.restore(
            resource,
            task.snapshot()
                .getRuntimeSnapshot());
        require(!runtime.grantReward("reward"), "Reload replay");
        CanonicalTaskInstance second = CanonicalTaskInstance
            .start(task.getPlayerUuid(), "story", "placement2", resource, 100);
        second.setLogicInput("gate", true, 101);
        require(second.grantReward("reward", 102), "Resource reuse isolated");
        require(
            !CanonicalTaskPlayerTransactions.key(task.snapshot(), "reward", "reward")
                .equals(CanonicalTaskPlayerTransactions.key(second.snapshot(), "reward", "reward")),
            "Placement receipt separation");
        com.google.gson.JsonObject ready = new com.google.gson.JsonParser().parse(json)
            .getAsJsonObject();
        com.google.gson.JsonObject edge = new com.google.gson.JsonObject();
        edge.addProperty("from_node_id", "in");
        edge.addProperty("from_port_id", "logic_out");
        edge.addProperty("to_node_id", "settle");
        edge.addProperty("to_port_id", "done");
        edge.addProperty("interface_kind", "logic");
        ready.getAsJsonObject("graph")
            .getAsJsonArray("connections")
            .add(edge);
        CanonicalGraphResource gated = new CanonicalGraphResourceLoader().load(
            ready.toString()
                .getBytes(StandardCharsets.UTF_8),
            "task.json",
            CanonicalGraphResourceKind.TASK);
        CanonicalTaskRuntime pending = CanonicalTaskRuntime.start(gated);
        pending.setLogicInput("gate", true);
        require(pending.isActive(), "Task settled before eligible reward delivered");
        pending = CanonicalTaskRuntime.restore(gated, pending.snapshot());
        require(pending.grantReward("reward") && pending.isSettled(), "Reward completion did not release settlement");
        require(
            CanonicalTaskRuntime.restore(gated, pending.snapshot())
                .isSettled(),
            "Settled receipt restore");
        System.out.println("TASK_REWARD_0330_LOGIC_RECEIPT_NBT_REUSE=PASS");
    }

    private static void itemVectors() throws Exception {
        Task0330Probe.main(new String[0]);
        net.minecraft.item.Item apple = (net.minecraft.item.Item) net.minecraft.item.Item.itemRegistry
            .getObject("minecraft:apple");
        net.minecraft.item.ItemStack prototype = new net.minecraft.item.ItemStack(apple, 1);
        net.minecraft.item.ItemStack[] slots = { new net.minecraft.item.ItemStack(apple, 63), null };
        require(CanonicalTaskPlayerTransactions.applyItem(slots, prototype, 65), "Multi-slot addition");
        require(slots[0].stackSize == 64 && slots[1].stackSize == 64, "Stack caps");
        net.minecraft.item.ItemStack[] staged = CanonicalTaskInventory.copy(slots);
        require(!CanonicalTaskPlayerTransactions.applyItem(staged, prototype, 1), "Full inventory accepted reward");
        require(slots[0].stackSize == 64 && slots[1].stackSize == 64, "Preparation mutated player slots");
        require(
            CanonicalTaskPlayerTransactions.applyItem(slots, prototype, Integer.MIN_VALUE) && slots[0] == null
                && slots[1] == null,
            "Negative item delta did not clamp");
        require(CanonicalTaskPlayerTransactions.applyItem(slots, prototype, 0) && slots[0] == null, "Zero not a no-op");
        System.out.println("TASK_REWARD_0330_SIGNED_ITEMS_CAPACITY_AND_ZERO=PASS");
    }

    private static void journal(Path root) throws Exception {
        Files.createDirectories(root);
        Path directory = Files.createTempDirectory(root, "crash-");
        UUID player = UUID.randomUUID();
        String first = repeat('a'), second = repeat('b');
        CanonicalTaskTransactionJournal journal = new CanonicalTaskTransactionJournal(directory);
        FakeState live = new FakeState();
        live.failApply = true;
        try {
            journal.commit(player, first, image(16, first), live);
            throw new AssertionError("Crash missing");
        } catch (IOException expected) {
            throw expected;
        } catch (InjectedCrash expected) {}
        require(live.count == 0, "Crash before apply changed live inventory");
        FakeState restart = new FakeState();
        require(new CanonicalTaskTransactionJournal(directory).recover(player, restart), "Prepared record recovery");
        require(restart.count == 16 && restart.checkpoints == 1, "After-image recovery");
        require(!journal.commit(player, first, image(32, first), restart), "Duplicate commit accepted");
        restart.count = 9; // Spending rewards after a successful save must survive recovery checks.
        require(!journal.recover(player, restart) && restart.count == 9, "Recovery replayed an already applied image");
        NBTTagCompound next = image(25, first);
        next.getCompoundTag("receipts")
            .setBoolean(second, true);
        restart.failCheckpoint = true;
        try {
            journal.commit(player, second, next, restart);
            throw new AssertionError("Checkpoint crash missing");
        } catch (IOException expected) {}
        FakeState oldPlayerSave = new FakeState();
        oldPlayerSave.apply(image(9, first));
        require(journal.recover(player, oldPlayerSave) && oldPlayerSave.count == 25, "Player save behind journal");
        FakeState veryOldSave = new FakeState();
        journal.recover(player, veryOldSave);
        require(
            veryOldSave.count == 25 && veryOldSave.hasReceipt(first) && veryOldSave.hasReceipt(second),
            "Cumulative receipt recovery");
        FakeState unrecovered = new FakeState();
        try {
            journal.commit(player, repeat('c'), image(100, repeat('c')), unrecovered);
            throw new AssertionError("Overwrote pending recovery");
        } catch (IOException expected) {}
        require(journal.recover(player, unrecovered) && unrecovered.count == 25, "Old journal lost");
        System.out.println("TASK_REWARD_0330_ATOMIC_JOURNAL_CRASH_WINDOWS=PASS");
    }

    private static final class InjectedCrash extends RuntimeException {
    }

    private static final class FakeState implements CanonicalTaskTransactionJournal.PlayerState {

        int count, checkpoints;
        boolean failApply, failCheckpoint;
        NBTTagCompound receipts = new NBTTagCompound();

        public boolean hasReceipt(String receipt) {
            return receipts.getBoolean(receipt);
        }

        public void apply(NBTTagCompound image) {
            if (failApply) throw new InjectedCrash();
            count = image.getInteger("count");
            receipts = (NBTTagCompound) image.getCompoundTag("receipts")
                .copy();
        }

        public void checkpoint() throws IOException {
            if (failCheckpoint) throw new IOException("Injected checkpoint failure");
            checkpoints++;
        }
    }

    private static NBTTagCompound image(int count, String receipt) {
        NBTTagCompound image = new NBTTagCompound();
        image.setInteger("count", count);
        NBTTagCompound receipts = new NBTTagCompound();
        receipts.setBoolean(receipt, true);
        image.setTag("receipts", receipts);
        return image;
    }

    private static String repeat(char value) {
        char[] chars = new char[64];
        java.util.Arrays.fill(chars, value);
        return new String(chars);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
