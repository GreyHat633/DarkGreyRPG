package darkgrey.rpg.task.forge;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.UUID;

import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
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
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.story.canonical.forge.Stability0402Fixtures;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.persistence.CanonicalTaskTransactionJournal;
import darkgrey.rpg.task.persistence.TaskReceiptKey;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Independent mixed business model: A layer, never claimed as native entity interaction or network clients. */
public final class MixedBusiness0402Probe {

    private static final String STORY = Stability0402Fixtures.SOURCE;
    private static final String ITEM = STORY + "~item~coin";
    private static final String ACTOR = STORY + "~actor~clerk";
    private static final String[] TYPES = { "kill_entity", "collect_item", "submit_item" };
    private static final CanonicalGraphResource[] RESOURCES = { task(0), task(1), task(2) };
    private static ItemStack prototype;
    private static ItemIdentitySavedData identities;

    private MixedBusiness0402Probe() {}

    public static CanonicalGraphResource fixtureResource(int kind) {
        return RESOURCES[kind];
    }

    public static void main(String[] args) throws Exception {
        receiptGrammar();
        Item item = new Item();
        java.lang.reflect.Method register = Item.itemRegistry.getClass()
            .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
        register.setAccessible(true);
        register.invoke(Item.itemRegistry, 32042, "dgrprobe:mixed0402", item);
        prototype = new ItemStack(item, 1, 7);
        identities = new ItemIdentitySavedData();
        identities.bindItem(ITEM, ItemStackDefinition.capture(prototype));
        rewardHistory();
        for (int seed = 40201; seed <= 40205; seed++) model(seed);
        System.out.println(
            "MIXED_BUSINESS_0402=PASS layer=A seeds=40201..40205 steps=2000 worlds=2 users=4 inventory rewards receipts cold_restore failures zero_settlement");
    }

    private static void receiptGrammar() {
        require(!TaskReceiptKey.isValid(null), "Null receipt rejected");
        String valid = String.join("", Collections.nCopies(64, "a"));
        require(TaskReceiptKey.isValid(valid), "Lowercase receipt accepted");
        String alphabet = "0123456789abcdefABCDEFgh\n\r\u0000\uff10\uff41";
        Random random = new Random(402061);
        for (int round = 0; round < 10000; round++) {
            StringBuilder key = new StringBuilder();
            int length = 60 + random.nextInt(9);
            for (int index = 0; index < length; index++) key.append(alphabet.charAt(random.nextInt(alphabet.length())));
            require(
                TaskReceiptKey.isValid(key.toString()) == key.toString()
                    .matches("[0-9a-f]{64}"),
                "Receipt grammar unchanged");
        }
        for (int position = 0; position < 64; position++) {
            for (int index = 0; index < alphabet.length(); index++) {
                String key = valid.substring(0, position) + alphabet.charAt(index) + valid.substring(position + 1);
                require(
                    TaskReceiptKey.isValid(key) == key.matches("[0-9a-f]{64}"),
                    "Every receipt position retains its grammar");
            }
        }
    }

    private static void rewardHistory() {
        UUID user = new UUID(40201, 99);
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.bind(MixedBusiness0402Probe::resolve);
        data.start(user, STORY, TYPES[2], RESOURCES[2], 1);
        Map<String, String> values = new LinkedHashMap<>();
        values.put("item", ITEM);
        values.put("actor_id", ACTOR);
        data.sampleObjective(
            user,
            STORY,
            TYPES[2],
            "objective",
            new CanonicalTaskEvent("submit_item", values, 2),
            2,
            null);
        require(
            data.completedHistory(user)
                .tagCount() == 0,
            "Pending reward has no completion history");
        require(
            data.grantReward(user, STORY, TYPES[2], "reward", 3)
                .getStatus() == CanonicalTaskInstanceStatus.SETTLED,
            "Reward settles Task");
        require(
            data.completedHistory(user)
                .tagCount() == 1,
            "Reward settlement archives immediately");
        NBTTagCompound before = image(data);
        data.setDirty(false);
        require(data.grantReward(user, STORY, TYPES[2], "reward", 4) == null, "Repeated reward no-op");
        require(
            !data.isDirty() && before.equals(image(data))
                && data.completedHistory(user)
                    .tagCount() == 1,
            "Repeated reward does not duplicate history");
    }

    private static void model(int seed) throws Exception {
        Path root = Files.createTempDirectory("dgr0402-mixed-" + seed + "-");
        Random random = new Random(seed);
        World[] worlds = { new World(root.resolve("A")), new World(root.resolve("B")) };
        StringBuilder trace = new StringBuilder("seed,step,world,user,operation\n");
        for (int step = 0; step < 2000; step++) {
            int w = random.nextInt(2), u = random.nextInt(4), op = random.nextInt(12);
            UUID user = new UUID(seed, u + 1);
            World world = worlds[w];
            Player player = world.players[u];
            Expected expected = world.expected[u];
            trace.append(seed)
                .append(',')
                .append(step)
                .append(',')
                .append(w)
                .append(',')
                .append(u)
                .append(',')
                .append(op)
                .append('\n');
            try {
                if (op == 0 || !expected.exists) {
                    if (!expected.exists) {
                        expected.exists = true;
                        expected.run = seed * 100000L + step;
                        for (int kind = 0; kind < 3; kind++)
                            world.data.start(user, STORY, TYPES[kind], RESOURCES[kind], expected.run);
                    } else {
                        NBTTagCompound before = image(world.data);
                        world.data.start(user, STORY, TYPES[0], RESOURCES[0], expected.run + 1);
                        require(before.equals(image(world.data)), "Active duplicate start is no-op");
                    }
                } else if (op == 1) {
                    int delta = random.nextInt(6) - 2;
                    ItemStack[] next = CanonicalTaskInventory.copy(player.inventory);
                    require(CanonicalTaskPlayerTransactions.applyItem(next, prototype, delta), "Fixture has room");
                    player.inventory = next;
                    expected.inventory = Math.max(0, expected.inventory + delta);
                } else if (op == 2) {
                    world.data.dispatch(user, CanonicalTaskEvent.killEntity(ACTOR));
                    if (!expected.cancelled && expected.progress[0] < 3) expected.progress[0]++;
                } else if (op == 3) {
                    int actual = CanonicalTaskInventory.count(
                        player.inventory,
                        RESOURCES[1].getGraph()
                            .getNodes()
                            .get(0),
                        identities);
                    require(actual == expected.inventory, "Independent inventory count");
                    world.data.dispatch(user, CanonicalTaskEvent.collectItem(ITEM, actual));
                    if (!expected.cancelled && expected.progress[1] < 3)
                        expected.progress[1] = Math.min(3, expected.inventory);
                } else if (op == 4 || op == 5) {
                    submit(world, player, expected, user, System.currentTimeMillis(), op == 5);
                } else if (op == 6) {
                    reward(world, player, expected, user, System.currentTimeMillis());
                } else if (op == 7) {
                    world.data.cancelByStory(user, STORY);
                    expected.cancelled = true;
                } else if (op == 8) {
                    world.data.discardByPlayerStory(user, STORY);
                    expected.exists = false;
                    expected.cancelled = false;
                    expected.rewarded = false;
                    Arrays.fill(expected.progress, 0);
                } else if (op == 9) {
                    NBTTagCompound payload = image(world.data);
                    CanonicalTaskSavedData next = new CanonicalTaskSavedData();
                    next.readFromNBT(payload);
                    next.bind(MixedBusiness0402Probe::resolve);
                    require(payload.equals(image(next)), "Cold Task image unchanged");
                    world.data = next;
                    for (int i = 0; i < 4; i++) {
                        Player cold = new Player();
                        cold.apply(world.players[i].image());
                        world.players[i] = cold;
                        require(
                            !world.journal.recover(new UUID(seed, i + 1), cold),
                            "Saved receipt blocks duplicate recovery");
                    }
                } else if (op == 10) {
                    NBTTagCompound before = image(world.data);
                    world.data.setDirty(false);
                    world.data.bind(MixedBusiness0402Probe::resolve);
                    world.data.dispatch(user, CanonicalTaskEvent.killEntity(STORY + "~actor~unrelated"));
                    require(
                        !world.data.isDirty() && before.equals(image(world.data)),
                        "No-content bind and unmatched event preserve state");
                } else {
                    world.journal.recover(user, player);
                }
                for (World check : worlds) for (int n = 0; n < 4; n++) validate(check, n, new UUID(seed, n + 1));
            } catch (Exception | AssertionError failure) {
                Files.write(
                    root.resolve("failure-trace.csv"),
                    trace.toString()
                        .getBytes(java.nio.charset.StandardCharsets.UTF_8));
                throw new AssertionError("seed=" + seed + " step=" + step + " root=" + root, failure);
            }
        }
        Files.write(
            root.resolve("operations.csv"),
            trace.toString()
                .getBytes(java.nio.charset.StandardCharsets.UTF_8));
        System.out.println("MIXED_BUSINESS_SEED=" + seed + " PASS steps=2000 evidence=" + root);
    }

    private static void submit(World world, Player player, Expected expected, UUID user, long time, boolean fail)
        throws Exception {
        if (expected.cancelled || expected.progress[2] == 2 || expected.inventory < 2) return;
        ItemStack[] next = CanonicalTaskInventory.copy(player.inventory);
        require(
            CanonicalTaskInventory.removeExact(
                next,
                RESOURCES[2].getGraph()
                    .getNodes()
                    .get(0),
                identities,
                2),
            "Atomic exact removal");
        NBTTagCompound before = image(world.data), playerBefore = player.image();
        CanonicalTaskInstanceSnapshot task = world.data.getSnapshot(user, STORY, TYPES[2]);
        String receipt = CanonicalTaskPlayerTransactions.key(task, "objective", "submit");
        Map<String, String> values = new LinkedHashMap<>();
        values.put("item", ITEM);
        values.put("actor_id", ACTOR);
        try {
            world.data.sampleObjective(
                user,
                STORY,
                TYPES[2],
                "objective",
                new CanonicalTaskEvent("submit_item", values, 2),
                time,
                new CanonicalTaskSavedData.ObjectiveCommit() {

                    public void commit() {
                        if (fail) throw new IllegalStateException("injected-before-durable-commit");
                        NBTTagCompound after = player.image(next, player.xp);
                        after.getCompoundTag("receipts")
                            .setBoolean(receipt, true);
                        try {
                            require(world.journal.commit(user, receipt, after, player), "First receipt commits");
                        } catch (java.io.IOException failure) {
                            throw new IllegalStateException(failure);
                        }
                    }

                    public void rollback() {
                        player.apply(playerBefore);
                    }
                });
            require(!fail, "Injected failure must reject");
            expected.inventory -= 2;
            expected.progress[2] = 2;
            expected.receipts++;
        } catch (IllegalStateException failure) {
            require(fail && "injected-before-durable-commit".equals(failure.getMessage()), "Specific failure only");
            require(
                before.equals(image(world.data)) && playerBefore.equals(player.image()),
                "Failed submit preserves both states");
        }
    }

    private static void reward(World world, Player player, Expected expected, UUID user, long time) throws Exception {
        if (expected.cancelled || expected.progress[2] != 2) return;
        CanonicalTaskInstanceSnapshot task = world.data.getSnapshot(user, STORY, TYPES[2]);
        String receipt = CanonicalTaskPlayerTransactions.key(task, "reward", "reward");
        if (!player.hasReceipt(receipt)) {
            NBTTagCompound after = player.image(player.inventory, player.xp + 7);
            after.getCompoundTag("receipts")
                .setBoolean(receipt, true);
            require(world.journal.commit(user, receipt, after, player), "Reward transaction commits once");
            expected.xp += 7;
            expected.receipts++;
        }
        CanonicalTaskInstanceSnapshot changed = world.data.grantReward(user, STORY, TYPES[2], "reward", time);
        require((changed != null) == !expected.rewarded, "Reward runtime receipt is idempotent");
        expected.rewarded = true;
        require(!world.journal.commit(user, receipt, player.image(), player), "Duplicate transaction has no delta");
    }

    private static void validate(World world, int index, UUID user) {
        Expected expected = world.expected[index];
        Player player = world.players[index];
        int inventory = 0;
        for (ItemStack stack : player.inventory) if (stack != null) inventory += stack.stackSize;
        require(inventory == expected.inventory && player.xp == expected.xp, "Independent inventory and reward totals");
        require(
            player.receipts.func_150296_c()
                .size() == expected.receipts,
            "Cumulative receipts count");
        require(
            world.data.snapshots(user)
                .size() == (expected.exists ? 3 : 0),
            "World and user ownership");
        if (!expected.exists) return;
        for (int kind = 0; kind < 3; kind++) {
            CanonicalTaskInstanceSnapshot task = world.data.getSnapshot(user, STORY, TYPES[kind]);
            require(task.getActivationTime() == expected.run, "Run ledger");
            require(
                task.getRuntimeSnapshot()
                    .getProgress()
                    .get("objective") == expected.progress[kind],
                "Objective ledger " + kind);
            boolean settled = kind == 0 && expected.progress[0] == 3 || kind == 2 && expected.rewarded;
            CanonicalTaskInstanceStatus status = settled ? CanonicalTaskInstanceStatus.SETTLED
                : expected.cancelled ? CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
                    : CanonicalTaskInstanceStatus.ACTIVE;
            require(task.getStatus() == status, "Settlement, cancellation and zero-settlement ledger " + kind);
        }
    }

    private static CanonicalGraphResource resolve(String id) {
        for (CanonicalGraphResource resource : RESOURCES) if (resource.getId()
            .equals(id)) return resource;
        return null;
    }

    private static CanonicalGraphResource task(int kind) {
        Map<String, JsonElement> properties = fields(
            "objective_type",
            "\"" + TYPES[kind] + "\"",
            "description",
            "\"Mixed\"",
            "required",
            kind == 2 ? "2" : "3");
        if (kind == 0) properties.put("entity", json("\"" + ACTOR + "\""));
        else {
            properties.put("item", json("\"" + ITEM + "\""));
            properties.put("metadata", json("{}"));
            if (kind == 2) properties.put("actor_id", json("\"" + ACTOR + "\""));
        }
        List<CanonicalGraphNode> nodes = new ArrayList<>();
        List<CanonicalGraphConnection> edges = new ArrayList<>();
        nodes.add(
            new CanonicalGraphNode(
                "objective",
                "objective",
                "Objective",
                Collections.singletonList(port("logic_status", false)),
                properties));
        if (kind != 1) {
            nodes.add(
                new CanonicalGraphNode(
                    "done",
                    "settle",
                    "Done",
                    Collections.singletonList(port("logic_in", true)),
                    fields("port_id", "\"done\"", "display_name", "\"Done\"", "display_order", "0")));
            edges.add(
                new CanonicalGraphConnection(
                    "objective",
                    "logic_status",
                    "done",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC));
        }
        if (kind == 2) {
            nodes.add(
                new CanonicalGraphNode(
                    "reward",
                    "reward",
                    "Reward",
                    Collections.singletonList(port("logic_in", true)),
                    fields("entries", "[{\"type\":\"xp\",\"amount\":7}]")));
            edges.add(
                new CanonicalGraphConnection(
                    "objective",
                    "logic_status",
                    "reward",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC));
        }
        return new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.TASK,
            STORY + "~task~" + TYPES[kind],
            "Mixed",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphPort port(String id, boolean input) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            0);
    }

    private static Map<String, JsonElement> fields(String... pairs) {
        Map<String, JsonElement> result = new LinkedHashMap<>();
        for (int i = 0; i < pairs.length; i += 2) result.put(pairs[i], json(pairs[i + 1]));
        return result;
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse(value);
    }

    private static NBTTagCompound image(CanonicalTaskSavedData data) {
        NBTTagCompound result = new NBTTagCompound();
        data.writeToNBT(result);
        return result;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static final class World {

        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        final Player[] players = { new Player(), new Player(), new Player(), new Player() };
        final Expected[] expected = { new Expected(), new Expected(), new Expected(), new Expected() };
        final CanonicalTaskTransactionJournal journal;

        World(Path root) {
            data.bind(MixedBusiness0402Probe::resolve);
            journal = new CanonicalTaskTransactionJournal(root);
        }
    }

    private static final class Expected {

        boolean exists, cancelled, rewarded;
        long run;
        int inventory, xp, receipts;
        final int[] progress = new int[3];
    }

    private static final class Player implements CanonicalTaskTransactionJournal.PlayerState {

        ItemStack[] inventory = new ItemStack[36];
        int xp;
        NBTTagCompound receipts = new NBTTagCompound();

        public boolean hasReceipt(String key) {
            return receipts.getBoolean(key);
        }

        public void apply(NBTTagCompound image) {
            inventory = new ItemStack[36];
            net.minecraft.nbt.NBTTagList slots = image.getTagList("inventory", 10);
            for (int i = 0; i < slots.tagCount(); i++) {
                NBTTagCompound slot = slots.getCompoundTagAt(i);
                inventory[slot.getInteger("index")] = ItemStack.loadItemStackFromNBT(slot);
            }
            xp = image.getInteger("xp");
            receipts = (NBTTagCompound) image.getCompoundTag("receipts")
                .copy();
        }

        public void checkpoint() {}

        NBTTagCompound image() {
            return image(inventory, xp);
        }

        NBTTagCompound image(ItemStack[] stacks, int reward) {
            NBTTagCompound image = new NBTTagCompound();
            net.minecraft.nbt.NBTTagList slots = new net.minecraft.nbt.NBTTagList();
            for (int i = 0; i < stacks.length; i++) if (stacks[i] != null) {
                NBTTagCompound slot = new NBTTagCompound();
                stacks[i].writeToNBT(slot);
                slot.setInteger("index", i);
                slots.appendTag(slot);
            }
            image.setTag("inventory", slots);
            image.setInteger("xp", reward);
            image.setTag("receipts", receipts.copy());
            return image;
        }
    }
}
