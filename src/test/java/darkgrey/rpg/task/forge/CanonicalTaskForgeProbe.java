package darkgrey.rpg.task.forge;

import java.io.File;
import java.lang.reflect.Field;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.entity.projectile.EntityArrow;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.util.DamageSource;
import net.minecraft.util.EntityDamageSource;
import net.minecraft.util.EntityDamageSourceIndirect;
import net.minecraftforge.common.util.FakePlayer;

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
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;
import sun.misc.Unsafe;

/** Current DGR-only boundaries plus the retained manager, recovery and attribution contracts. */
public final class CanonicalTaskForgeProbe {

    private static final String OWNER = "ST-2345-6789-ABCD-EFGH";
    private static final String ACTOR = OWNER + "~actor~bandits";
    private static final String ITEM = OWNER + "~item~coin";
    private static final String GROUP = OWNER + "~item_group~coins";
    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");
    private static final UUID OTHER = UUID.fromString("00000000-0000-0000-0000-000000000002");

    private CanonicalTaskForgeProbe() {}

    public static void main(String[] args) throws Exception {
        darkgrey.rpg.graph.canonical.TaskTargetContract0400Probe.run();
        identityEvents();
        inMemoryRejection();
        inventoryAndCollection();
        managerRecovery();
        worldLogicLifecycle();
        lethalAttribution();
        System.out.println("CANONICAL_TASK_0400_DGR_ONLY_EVENTS_RUNTIME_INVENTORY=PASS");
        System.out.println("CANONICAL_TASK_0400_MANAGER_PLAYER_ISOLATION_JOURNAL_RECOVERY=PASS");
        System.out.println("CANONICAL_TASK_0400_LETHAL_ATTRIBUTION_STICKY_PREREQUISITE=PASS");
    }

    private static void identityEvents() {
        String individual = OWNER + "~actor~individual";
        String foreign = "ST-JKLM-NPQR-STUV-WXYZ~actor~bandits";
        List<CanonicalTaskEvent> events = CanonicalTaskForgeEventNormalizer
            .killEventsForActors(Arrays.asList(ACTOR, individual, ACTOR, foreign, "minecraft:zombie", "", null, ITEM));
        require(events.size() == 3, "All distinct Actor identities retained, registry and wrong Kind rejected");
        require(
            ACTOR.equals(
                events.get(0)
                    .get("entity"))
                && individual.equals(
                    events.get(1)
                        .get("entity"))
                && foreign.equals(
                    events.get(2)
                        .get("entity")),
            "Stable identity order and owner isolation");
        require(
            CanonicalTaskForgeEventNormalizer.killEventsForActors(Collections.<String>emptyList())
                .isEmpty(),
            "Unnominated entity generates no event");
    }

    private static void inMemoryRejection() {
        for (String type : Arrays.asList("kill_entity", "collect_item", "submit_item", "interact_actor")) {
            String valid = type.equals("kill_entity") || type.equals("interact_actor") ? ACTOR : ITEM;
            CanonicalTaskRuntime.start(task(type, valid, false));
            for (String bad : Arrays.asList("minecraft:zombie", "mod:custom_item", OWNER + "~session~wrong", "broken"))
                reject(() -> CanonicalTaskRuntime.start(task(type, bad, false)));
            CanonicalTaskRuntime dormant = CanonicalTaskRuntime.start(task(type, "", false));
            require(dormant.isActive(), "Unconnected empty target remains dormant");
        }
        CanonicalTaskRuntime.start(task("collect_item", GROUP, false));
        reject(() -> CanonicalTaskRuntime.start(task("kill_entity", ITEM, false)));
        reject(() -> CanonicalTaskRuntime.start(task("collect_item", ACTOR, false)));
    }

    private static void inventoryAndCollection() throws Exception {
        Item item = new Item();
        java.lang.reflect.Method register = Item.itemRegistry.getClass()
            .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
        register.setAccessible(true);
        register.invoke(Item.itemRegistry, 32040, "dgrprobe:coin0400", item);
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString("quality", "authored");
        ItemStack exact = new ItemStack(item, 2, 7);
        exact.setTagCompound(tag);
        ItemStack different = new ItemStack(item, 3, 8);
        ItemIdentitySavedData identities = new ItemIdentitySavedData();
        ItemStackDefinition definition = ItemStackDefinition.capture(exact);
        identities.bindItem(ITEM, definition);
        identities.addGroupMember(GROUP, new ItemGroupMember(ItemMatchMode.FUZZY, definition));
        String exactGroup = OWNER + "~item_group~exact";
        identities.addGroupMember(exactGroup, new ItemGroupMember(ItemMatchMode.EXACT, definition));
        require(CanonicalTaskInventory.matches(exact, objective("collect_item", ITEM), identities), "Exact Item");
        require(
            !CanonicalTaskInventory.matches(different, objective("collect_item", ITEM), identities),
            "Damage/NBT exactness");
        require(
            CanonicalTaskInventory.matches(different, objective("collect_item", GROUP), identities),
            "FUZZY binding retained");
        require(
            !CanonicalTaskInventory.matches(different, objective("collect_item", exactGroup), identities),
            "EXACT group retained");
        require(
            !CanonicalTaskInventory.matches(exact, objective("collect_item", "dgrprobe:coin0400"), identities),
            "No registry shortcut");
        require(
            !CanonicalTaskInventory.matches(exact, objective("collect_item", ITEM), new ItemIdentitySavedData()),
            "No identity by species");
        CanonicalGraphNode unrestricted = objective("collect_item", GROUP);
        Map<String, JsonElement> restrictedProperties = new LinkedHashMap<String, JsonElement>(
            unrestricted.getProperties());
        restrictedProperties.put("metadata", new JsonParser().parse("{\"damage\":\"7\"}"));
        CanonicalGraphNode additional = new CanonicalGraphNode(
            unrestricted.getId(),
            unrestricted.getType(),
            unrestricted.getDisplayName(),
            unrestricted.getPorts(),
            restrictedProperties);
        require(!CanonicalTaskInventory.matches(different, additional, identities), "Damage restriction narrows FUZZY");
        ItemStack[] inventory = { exact.copy(), different.copy(), null };
        CanonicalGraphNode submit = objective("submit_item", GROUP);
        require(CanonicalTaskInventory.count(inventory, submit, identities) == 5, "Across slots");
        require(!CanonicalTaskInventory.removeExact(inventory, submit, identities, 6), "Shortage rejects");
        require(inventory[0].stackSize == 2 && inventory[1].stackSize == 3, "Shortage changes no slot");
        require(CanonicalTaskInventory.removeExact(inventory, submit, identities, 4), "Exact required count removed");
        require(CanonicalTaskInventory.count(inventory, submit, identities) == 1, "Only required amount removed");
        ItemStack[] rewardInventory = { new ItemStack(item, 63, 7), null };
        ItemStack prototype = new ItemStack(item, 1, 7);
        require(
            CanonicalTaskPlayerTransactions.applyItem(rewardInventory, prototype, 65),
            "Positive reward across slots");
        require(rewardInventory[0].stackSize == 64 && rewardInventory[1].stackSize == 64, "Reward respects stack caps");
        require(
            !CanonicalTaskPlayerTransactions.applyItem(CanonicalTaskInventory.copy(rewardInventory), prototype, 1),
            "Full inventory rejects addition");
        require(
            CanonicalTaskPlayerTransactions.applyItem(rewardInventory, prototype, 0)
                && rewardInventory[0].stackSize == 64,
            "Zero reward is a no-op");
        require(
            CanonicalTaskPlayerTransactions.applyItem(rewardInventory, prototype, Integer.MIN_VALUE)
                && rewardInventory[0] == null
                && rewardInventory[1] == null,
            "Negative reward clamps without overflow");
        CanonicalTaskRuntime collect = CanonicalTaskRuntime.start(task("collect_item", GROUP, false));
        collect.accept(CanonicalTaskEvent.collectItem(GROUP, 1));
        collect.accept(CanonicalTaskEvent.collectItem(GROUP, 0));
        require(
            collect.getProgress()
                .get("objective") == 0,
            "Current snapshot, not lifetime pickup");
        collect.accept(CanonicalTaskEvent.collectItem(GROUP, 2));
        collect.accept(CanonicalTaskEvent.collectItem(GROUP, 0));
        require(
            collect.getProgress()
                .get("objective") == 2 && collect.isActive(),
            "Completed collection latches; zero settlement stays ACTIVE");
    }

    private static void managerRecovery() {
        CanonicalGraphResource resource = task("kill_entity", ACTOR, true);
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<String, CanonicalGraphResource>();
        tasks.put(resource.getId(), resource);
        ProjectSnapshot project = new ProjectSnapshot(
            new ProjectDefinition(3, "probe", "Probe"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(Collections.emptyMap(), Collections.emptyMap(), tasks, Collections.emptyMap()));
        CanonicalTaskForgeManager manager = new CanonicalTaskForgeManager(new ProjectRepository(new File(".")));
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        CanonicalTaskInstanceSnapshot first = manager
            .startTrustedForProbe(PLAYER, project, data, OWNER, "placement", resource.getId());
        CanonicalTaskInstanceSnapshot again = manager
            .startTrustedForProbe(PLAYER, project, data, OWNER, "placement", resource.getId());
        require(first.getActivationTime() == again.getActivationTime() && data.size() == 1, "Reentry reuses instance");
        manager.startTrustedForProbe(OTHER, project, data, OWNER, "placement", resource.getId());
        require(
            manager.dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity("minecraft:zombie"))
                .getChangedInstanceCount() == 0,
            "Registry event cannot advance typed target");
        manager.dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity(ACTOR));
        require(
            manager.snapshotTrustedForProbe(OTHER, project, data, OWNER, "placement")
                .getRuntimeSnapshot()
                .getProgress()
                .get("objective") == 0,
            "Other player unaffected");
        require(
            manager.journalTrustedForProbe(PLAYER, project, data)
                .size() == 1,
            "Current journal projection");
        NBTTagCompound raw = new NBTTagCompound();
        data.writeToNBT(raw);
        CanonicalTaskSavedData restored = new CanonicalTaskSavedData();
        restored.readFromNBT(raw);
        require(!restored.isBound(), "Restoration waits for current definition");
        manager.snapshotsTrustedForProbe(PLAYER, project, restored);
        Object index = restored.getSubscriptionIndex();
        manager.snapshotsTrustedForProbe(PLAYER, project, restored);
        require(index == restored.getSubscriptionIndex(), "No redundant index rebuild");
        manager.dispatchTrustedForProbe(PLAYER, project, restored, CanonicalTaskEvent.killEntity(ACTOR));
        require(
            "done".equals(
                manager.snapshotTrustedForProbe(PLAYER, project, restored, OWNER, "placement")
                    .getRuntimeSnapshot()
                    .getResultPortId()),
            "Partial progress restored and settles exactly");
        reject(() -> manager.startTrustedForProbe(PLAYER, project, restored, OWNER, "missing", "missing"));
    }

    private static void worldLogicLifecycle() {
        CanonicalGraphResource plain = task("kill_entity", ACTOR, false);
        CanonicalGraphNode objective = plain.getGraph()
            .getNodes()
            .get(0);
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>(objective.getProperties());
        properties.put("prerequisite_enabled", json("true"));
        CanonicalGraphNode gated = new CanonicalGraphNode(
            "objective",
            "objective",
            "Objective",
            Arrays.asList(port("prerequisite", true), port("logic_status", false)),
            properties);
        CanonicalGraphNode gate = new CanonicalGraphNode(
            "gate",
            "logic_input",
            "Night",
            Arrays.asList(port("logic_out", false)),
            fields("port_id", "\"night\"", "display_name", "\"Night\"", "source", "\"minecraft:night\""));
        CanonicalGraphResource resource = new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.TASK,
            plain.getId(),
            "Task",
            new CanonicalGraph(
                Arrays.asList(gate, gated),
                Arrays.asList(
                    new CanonicalGraphConnection(
                        "gate",
                        "logic_out",
                        "objective",
                        "prerequisite",
                        CanonicalGraphInterfaceKind.LOGIC))));
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        require(!runtime.accept(CanonicalTaskEvent.killEntity(ACTOR)), "False prerequisite waits");
        runtime.setLogicInput("night", true);
        require(runtime.accept(CanonicalTaskEvent.killEntity(ACTOR)), "True prerequisite activates");
        runtime.setLogicInput("night", false);
        require(runtime.accept(CanonicalTaskEvent.killEntity(ACTOR)), "Activated prerequisite remains sticky");
        require(
            CanonicalTaskRuntime.restore(resource, runtime.snapshot())
                .getProgress()
                .get("objective") == 2,
            "Sticky progress restores");
    }

    private static void lethalAttribution() throws Exception {
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
            require(
                CanonicalTaskForgeEventNormalizer.creditedKiller(source) == null,
                "No fake or environmental credit");
    }

    private static CanonicalGraphNode objective(String type, String target) {
        Map<String, JsonElement> properties = fields(
            "objective_type",
            "\"" + type + "\"",
            "description",
            "\"minecraft:zombie\"");
        if (!"interact_actor".equals(type)) properties.put("required", json("2"));
        if ("kill_entity".equals(type)) properties.put("entity", json("\"" + target + "\""));
        else if ("interact_actor".equals(type)) properties.put("actor_id", json("\"" + target + "\""));
        else {
            properties.put("item", json("\"" + target + "\""));
            properties.put("metadata", json("{}"));
            if ("submit_item".equals(type)) properties.put("actor_id", json("\"" + ACTOR + "\""));
        }
        return new CanonicalGraphNode(
            "objective",
            "objective",
            "Objective",
            Arrays.asList(port("logic_status", false)),
            properties);
    }

    private static CanonicalGraphResource task(String type, String target, boolean settle) {
        CanonicalGraphNode objective = objective(type, target);
        CanonicalGraphNode done = new CanonicalGraphNode(
            "done",
            "settle",
            "Done",
            Arrays.asList(port("logic_in", true)),
            fields("port_id", "\"done\"", "display_name", "\"Done\"", "display_order", "0"));
        return new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.TASK,
            OWNER + "~task~probe",
            "Task",
            new CanonicalGraph(
                settle ? Arrays.asList(objective, done) : Arrays.asList(objective),
                settle ? Arrays.asList(
                    new CanonicalGraphConnection(
                        "objective",
                        "logic_status",
                        "done",
                        "logic_in",
                        CanonicalGraphInterfaceKind.LOGIC))
                    : Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphPort port(String id, boolean input) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            0);
    }

    private static Map<String, JsonElement> fields(String... values) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) properties.put(values[i], json(values[i + 1]));
        return properties;
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse(value);
    }

    private static void reject(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException | darkgrey.rpg.graph.canonical.CanonicalGraphResourceException expected) {
            return;
        }
        throw new AssertionError("Invalid input accepted");
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
