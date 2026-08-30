package darkgrey.rpg.task.forge;

import java.io.File;
import java.lang.reflect.Method;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Focused offline proof of the Stage 4 Forge manager, adapter, and normalization boundary. */
public final class CanonicalTaskForgeProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");
    private static final UUID OTHER = UUID.fromString("00000000-0000-0000-0000-000000000002");

    private CanonicalTaskForgeProbe() {}

    public static void main(String[] args) {
        normalization();
        managerLifecycle();
        worldLogicLifecycle();
        adapterShape();
        System.out.println("CANONICAL_TASK_FORGE_NORMALIZATION=PASS");
        System.out.println("CANONICAL_TASK_MULTI_INTERACTION_DISPATCH=PASS");
        System.out.println("CANONICAL_TASK_FORGE_MANAGER_BIND_START_JOURNAL=PASS");
        System.out.println("CANONICAL_TASK_WORLD_LOGIC_LIFECYCLE=PASS");
    }

    private static void normalization() {
        require(
            "minecraft:slime".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Slime")),
            "slime normalization");
        require(
            "minecraft:cave_spider".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("CaveSpider")),
            "cave spider normalization");
        require(
            "minecraft:zombie_pigman".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("PigZombie")),
            "pig zombie normalization");
        require(
            "minecraft:ender_dragon".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("EnderDragon")),
            "ender dragon normalization");
        require(
            "minecraft:creeper".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Creeper")),
            "creeper normalization");
        require(
            "minecraft:zombie".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Zombie")),
            "zombie normalization");
        require(
            "minecraft:skeleton".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Skeleton")),
            "skeleton normalization");
        require(
            "minecraft:squid".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Squid")),
            "squid normalization");
        require(
            "minecraft:magma_cube".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("LavaSlime")),
            "lava slime normalization");
        require(
            "minecraft:wither".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("WitherBoss")),
            "wither normalization");
        require(
            "minecraft:mooshroom".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("MushroomCow")),
            "mushroom cow normalization");
        require(
            "minecraft:ocelot".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("Ozelot")),
            "ozelot normalization");
        require(
            "minecraft:iron_golem".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("VillagerGolem")),
            "villager golem normalization");
        require(
            "minecraft:horse".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("EntityHorse")),
            "horse normalization");
        require(
            "examplemod:mymob".equals(CanonicalTaskForgeEventNormalizer.normalizeEntityId("ExampleMod:MyMob")),
            "namespaced mod normalization");
        require(CanonicalTaskForgeEventNormalizer.normalizeEntityId("UnknownMob") == null, "unknown entity rejected");
        require(CanonicalTaskForgeEventNormalizer.normalizeEntityId(" ") == null, "blank entity ignored");

        List<CanonicalTaskEvent> kills = CanonicalTaskForgeEventNormalizer
            .killEventsForIds(" Minecraft:Zombie ", Arrays.asList("guards", "guard_1", "guards", "", null, "guard_1"));
        require(kills.size() == 3, "multi-kill deduplication");
        require(
            "minecraft:zombie".equals(
                kills.get(0)
                    .get("entity")),
            "multi-kill registry ordering");
        require(
            "guards".equals(
                kills.get(1)
                    .get("entity")),
            "multi-kill group ordering");
        require(
            "guard_1".equals(
                kills.get(2)
                    .get("entity")),
            "multi-kill identity ordering");
        require(
            CanonicalTaskForgeEventNormalizer.killEventsForIds(null, Arrays.asList(" ", "guards"))
                .size() == 1,
            "identity-only kill event");

        CanonicalTaskEvent collect = CanonicalTaskForgeEventNormalizer.collect("Minecraft:Iron", 7, 3);
        require(
            collect != null && CanonicalTaskEvent.COLLECT_ITEM.equals(collect.getType())
                && "minecraft:iron".equals(collect.get("item"))
                && "7".equals(collect.get("damage"))
                && collect.getAmount() == 3,
            "collect mapping");
        require(CanonicalTaskForgeEventNormalizer.collect("iron", 0, 1) == null, "unnamespaced collect rejected");
        require(CanonicalTaskForgeEventNormalizer.collect("minecraft:iron", -1, 1) == null, "invalid damage rejected");
        List<CanonicalTaskEvent> pickup = CanonicalTaskForgeEventNormalizer.collectEventsForIds(
            "Minecraft:Iron",
            7,
            3,
            Arrays.asList("royal_key", "royal_key", "alternate_key"),
            Arrays.asList("sword", "royal_key", "weapon", "sword"));
        require(pickup.size() == 5, "pickup multi-target deduplication");
        require(
            "minecraft:iron".equals(
                pickup.get(0)
                    .get("item")),
            "pickup registry target first");
        require(
            "royal_key".equals(
                pickup.get(1)
                    .get("item")),
            "pickup Item ID ordering");
        require(
            "alternate_key".equals(
                pickup.get(2)
                    .get("item")),
            "pickup second Item ID ordering");
        require(
            "sword".equals(
                pickup.get(3)
                    .get("item")),
            "pickup Group ordering");
        require(
            "weapon".equals(
                pickup.get(4)
                    .get("item")),
            "pickup second Group ordering");
        for (CanonicalTaskEvent event : pickup)
            require(event.getAmount() == 3 && "7".equals(event.get("damage")), "pickup amount and damage metadata");
        CanonicalTaskEvent interact = CanonicalTaskForgeEventNormalizer.interact(" actor_7 ");
        require(interact != null && "actor_7".equals(interact.get("actor_id")), "interact mapping");
        require(CanonicalTaskForgeEventNormalizer.interact(" ") == null, "blank actor rejected");

        List<CanonicalTaskEvent> interactions = CanonicalTaskForgeEventNormalizer
            .interactEventsForIds(Arrays.asList(" primary ", "guards", "primary", "", "guards", null, "town"));
        require(interactions.size() == 3, "multi-interaction deduplication");
        require(
            "primary".equals(
                interactions.get(0)
                    .get("actor_id")),
            "multi-interaction primary ordering");
        require(
            "guards".equals(
                interactions.get(1)
                    .get("actor_id")),
            "multi-interaction group ordering");
        require(
            "town".equals(
                interactions.get(2)
                    .get("actor_id")),
            "multi-interaction tail ordering");
    }

    private static void managerLifecycle() {
        CanonicalGraphResource resource = taskResource("task_a");
        ProjectSnapshot project = project(resource);
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        CanonicalTaskForgeManager manager = new CanonicalTaskForgeManager(new ProjectRepository(new File(".")));

        CanonicalTaskInstanceSnapshot first = manager
            .startTrustedForProbe(PLAYER, project, data, "story", "placement", "task_a");
        CanonicalTaskInstanceSnapshot reentry = manager
            .startTrustedForProbe(PLAYER, project, data, "story", "placement", "task_a");
        require(first.getActivationTime() == reentry.getActivationTime() && data.size() == 1, "start re-entry");
        CanonicalTaskDispatchResult dispatched = manager
            .dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity("minecraft:slime"));
        require(dispatched.getCandidateCount() == 1 && dispatched.getChangedInstanceCount() == 1, "indexed dispatch");

        data.start(OTHER, "story", "placement", resource, 1L);
        require(
            manager.snapshotsTrustedForProbe(PLAYER, project, data)
                .size() == 1,
            "player projection");
        require(
            manager.snapshotsTrustedForProbe(OTHER, project, data)
                .size() == 1,
            "other player projection");
        require(
            manager.journalTrustedForProbe(PLAYER, project, data)
                .size() == 1,
            "journal projection");

        CanonicalTaskSavedData pending = new CanonicalTaskSavedData("pending");
        net.minecraft.nbt.NBTTagCompound raw = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(raw);
        pending.readFromNBT(raw);
        require(!pending.isBound(), "pending starts unbound");
        manager.snapshotsTrustedForProbe(PLAYER, project, pending);
        Object firstIndex = pending.getSubscriptionIndex();
        require(pending.isBound(), "first access binds");
        manager.snapshotsTrustedForProbe(PLAYER, project, pending);
        require(firstIndex == pending.getSubscriptionIndex(), "second access does not rebuild");

        expectFailure(new Runnable() {

            @Override
            public void run() {
                manager.startTrustedForProbe(PLAYER, project, data, "story", "placement", "missing");
            }
        }, "missing Task rejection");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                manager.snapshotTrustedForProbe(PLAYER, project, data, " ", "placement");
            }
        }, "strict snapshot identity");

        CanonicalGraphResource guards = taskResource("task_guards", "guards");
        ProjectSnapshot guardsProject = project(guards);
        CanonicalTaskSavedData guardsData = new CanonicalTaskSavedData();
        manager.startTrustedForProbe(PLAYER, guardsProject, guardsData, "story", "placement", "task_guards");
        CanonicalTaskDispatchResult nonGuards = dispatchAll(
            manager,
            PLAYER,
            guardsProject,
            guardsData,
            CanonicalTaskForgeEventNormalizer.killEventsForIds("minecraft:zombie", Collections.singletonList("town")));
        require(nonGuards.getChangedInstanceCount() == 0, "non-guards kill ignored");
        CanonicalTaskDispatchResult guardsKill = dispatchAll(
            manager,
            PLAYER,
            guardsProject,
            guardsData,
            CanonicalTaskForgeEventNormalizer
                .killEventsForIds("minecraft:zombie", Collections.singletonList("guards")));
        require(guardsKill.getChangedInstanceCount() == 1, "guards kill advances objective");
    }

    private static CanonicalTaskDispatchResult dispatchAll(CanonicalTaskForgeManager manager, UUID player,
        ProjectSnapshot project, CanonicalTaskSavedData data, List<CanonicalTaskEvent> events) {
        CanonicalTaskDispatchResult result = null;
        for (CanonicalTaskEvent event : events) result = manager.dispatchTrustedForProbe(player, project, data, event);
        return result;
    }

    private static void adapterShape() {
        int handlers = 0;
        for (Method method : CanonicalTaskEventAdapter.class.getDeclaredMethods()) {
            SubscribeEvent annotation = method.getAnnotation(SubscribeEvent.class);
            if (annotation == null) continue;
            handlers++;
            require(method.getParameterTypes().length == 1, "event handler arity");
            String type = method.getParameterTypes()[0].getName();
            require(
                type.equals("net.minecraftforge.event.entity.living.LivingDeathEvent")
                    || type.equals("net.minecraftforge.event.entity.player.EntityItemPickupEvent")
                    || type.equals("net.minecraftforge.event.entity.player.EntityInteractEvent")
                    || type.equals("cpw.mods.fml.common.gameevent.TickEvent$PlayerTickEvent"),
                "event handler type");
        }
        require(handlers == 4, "exactly four task handlers");
    }

    private static void worldLogicLifecycle() {
        CanonicalGraphResource resource = worldBoundTaskResource();
        ProjectSnapshot project = project(resource);
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        CanonicalTaskForgeManager manager = new CanonicalTaskForgeManager(new ProjectRepository(new File(".")));
        CanonicalTaskInstanceSnapshot task = manager
            .startTrustedForProbe(PLAYER, project, data, "story", "night_kills", resource.getId());

        task = manager.synchronizeWorldLogicTrustedForProbe(PLAYER, project, data, task, 13000L);
        for (int count = 0; count < 7; count++)
            manager.dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity("minecraft:slime"));
        task = manager.snapshotTrustedForProbe(PLAYER, project, data, "story", "night_kills");
        require(
            task.getRuntimeSnapshot()
                .getProgress()
                .get("kill")
                .intValue() == 7,
            "night progress reaches seven");

        task = manager.synchronizeWorldLogicTrustedForProbe(PLAYER, project, data, task, 1000L);
        for (int count = 0; count < 3; count++)
            manager.dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity("minecraft:slime"));
        task = manager.snapshotTrustedForProbe(PLAYER, project, data, "story", "night_kills");
        require(
            task.getRuntimeSnapshot()
                .getProgress()
                .get("kill")
                .intValue() == 7,
            "day events do not advance a night-gated objective");

        task = manager.synchronizeWorldLogicTrustedForProbe(PLAYER, project, data, task, 13000L);
        for (int count = 0; count < 3; count++)
            manager.dispatchTrustedForProbe(PLAYER, project, data, CanonicalTaskEvent.killEntity("minecraft:slime"));
        task = manager.snapshotTrustedForProbe(PLAYER, project, data, "story", "night_kills");
        require(
            task.getRuntimeSnapshot()
                .getProgress()
                .get("kill")
                .intValue() == 10,
            "progress resumes from seven after night returns");
        require(
            task.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED,
            "night-gated Task did not settle at ten");
    }

    private static ProjectSnapshot project(CanonicalGraphResource resource) {
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<String, CanonicalGraphResource>();
        tasks.put(resource.getId(), resource);
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(Collections.emptyMap(), Collections.emptyMap(), tasks, Collections.emptyMap()));
    }

    private static CanonicalGraphResource taskResource(String id) {
        return taskResource(id, "minecraft:slime");
    }

    private static CanonicalGraphResource taskResource(String id, String target) {
        CanonicalGraphNode activate = node(
            "activate",
            "activate",
            Collections.singletonList(port("logic_out", false, 0)),
            Collections.<String, JsonElement>emptyMap());
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("objective_type", json(CanonicalTaskEvent.KILL_ENTITY));
        properties.put("description", json("Kill " + target));
        properties.put("required", json("1"));
        properties.put("entity", json(target));
        CanonicalGraphNode objective = node(
            "kill",
            "objective",
            Arrays.asList(port("logic_enable", true, 0), port("logic_status", false, 1)),
            properties);
        CanonicalGraphNode settle = node(
            "settle",
            "settle",
            Collections.singletonList(port("done", true, 0)),
            Collections.<String, JsonElement>emptyMap());
        List<CanonicalGraphConnection> edges = Arrays.asList(
            new CanonicalGraphConnection(
                "activate",
                "logic_out",
                "kill",
                "logic_enable",
                CanonicalGraphInterfaceKind.LOGIC),
            new CanonicalGraphConnection("kill", "logic_status", "settle", "done", CanonicalGraphInterfaceKind.LOGIC));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            id,
            "Forge Probe Task",
            new CanonicalGraph(Arrays.asList(activate, objective, settle), edges));
    }

    private static CanonicalGraphResource worldBoundTaskResource() {
        Map<String, JsonElement> inputProperties = new LinkedHashMap<String, JsonElement>();
        inputProperties.put("port_id", json("night"));
        inputProperties.put("display_name", json("Night"));
        inputProperties.put("source", json("minecraft:night"));
        CanonicalGraphNode input = node(
            "night_input",
            "logic_input",
            Collections.singletonList(port("logic_out", false, 0)),
            inputProperties);
        Map<String, JsonElement> objectiveProperties = new LinkedHashMap<String, JsonElement>();
        objectiveProperties.put("objective_type", json(CanonicalTaskEvent.KILL_ENTITY));
        objectiveProperties.put("description", json("Kill slimes at night"));
        objectiveProperties.put("required", json("10"));
        objectiveProperties.put("entity", json("minecraft:slime"));
        CanonicalGraphNode objective = node(
            "kill",
            "objective",
            Arrays.asList(port("logic_enable", true, 0), port("logic_status", false, 1)),
            objectiveProperties);
        CanonicalGraphNode settle = node(
            "settle",
            "settle",
            Collections.singletonList(port("done", true, 0)),
            Collections.<String, JsonElement>emptyMap());
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "night_task",
            "Night Task",
            new CanonicalGraph(
                Arrays.asList(input, objective, settle),
                Arrays.asList(
                    new CanonicalGraphConnection(
                        "night_input",
                        "logic_out",
                        "kill",
                        "logic_enable",
                        CanonicalGraphInterfaceKind.LOGIC),
                    new CanonicalGraphConnection(
                        "kill",
                        "logic_status",
                        "settle",
                        "done",
                        CanonicalGraphInterfaceKind.LOGIC))));
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static CanonicalGraphPort port(String id, boolean input, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse(value.matches("[0-9]+") ? value : "\"" + value + "\"");
    }

    private static void expectFailure(Runnable action, String label) {
        try {
            action.run();
            throw new IllegalStateException("Expected failure: " + label);
        } catch (IllegalArgumentException expected) {}
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
