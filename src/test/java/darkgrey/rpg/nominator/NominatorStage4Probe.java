package darkgrey.rpg.nominator;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityBind;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryBind;
import darkgrey.rpg.network.message.nominator.S2CNominatorEntityOpen;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.StoryDefinition;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Focused Stage 4 probe: search, permission, individual/group validation, and conflict handling. */
public final class NominatorStage4Probe {

    private NominatorStage4Probe() {}

    public static void main(String[] args) {
        LinkedHashMap<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            "hero",
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_INDIVIDUAL,
                "hero",
                "Hero",
                "",
                Collections.<String>emptyList(),
                "kingdom"));
        actors.put(
            "townfolk",
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_COLLECTIVE,
                "townfolk",
                "Townfolk",
                "",
                Collections.<String>emptyList(),
                "kingdom"));
        LinkedHashMap<String, StoryDefinition> stories = new LinkedHashMap<String, StoryDefinition>();
        stories.put(
            "kingdom",
            new StoryDefinition(
                1,
                "kingdom",
                "Kingdom",
                "",
                Collections.emptyList(),
                Collections.emptyList(),
                "",
                Arrays.asList("main")));
        ProjectSnapshot snapshot = new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            stories,
            darkgrey.rpg.graph.canonical.CanonicalProjectContent.empty());
        require(
            NominatorStorySearch.actors(snapshot, "kingdom", "hero")
                .size() == 1,
            "story search");
        NpcIdentitySavedData identities = new NpcIdentitySavedData("probe_npc");
        NominatorSavedData selections = new NominatorSavedData("probe_nominator");
        UUID entity = UUID.randomUUID();
        require(
            !NominatorService
                .bindEntity(
                    false,
                    entity,
                    "probe",
                    0,
                    "hero",
                    Collections.<String>emptyList(),
                    "kingdom",
                    snapshot,
                    identities,
                    selections)
                .isAccepted(),
            "permission");
        require(
            NominatorService
                .bindEntity(
                    true,
                    entity,
                    "probe",
                    0,
                    "hero",
                    Arrays.asList("townfolk"),
                    "kingdom",
                    snapshot,
                    identities,
                    selections)
                .isAccepted(),
            "binding");
        require(
            !NominatorService
                .bindEntity(
                    true,
                    entity,
                    "probe",
                    0,
                    "other",
                    Collections.<String>emptyList(),
                    "kingdom",
                    snapshot,
                    identities,
                    selections)
                .isAccepted(),
            "conflict");
        UUID replacement = UUID.randomUUID();
        require(
            NominatorService
                .bindEntity(
                    true,
                    replacement,
                    "probe",
                    0,
                    "hero",
                    Collections.<String>emptyList(),
                    "kingdom",
                    true,
                    snapshot,
                    identities,
                    selections)
                .isAccepted()
                && identities.getHost("hero")
                    .getEntityUuid()
                    .equals(replacement)
                && selections.get(entity)
                    .getIndividualId() == null
                && selections.get(entity)
                    .getGroupIds()
                    .equals(Arrays.asList("townfolk")),
            "explicit transfer clears old individual");
        require(
            NominatorService
                .bindEntity(
                    true,
                    entity,
                    "probe",
                    0,
                    null,
                    Arrays.asList("townfolk"),
                    "kingdom",
                    snapshot,
                    identities,
                    selections)
                .isAccepted()
                && selections.get(entity)
                    .getIndividualId() == null,
            "clear individual retaining groups");
        require(
            NominatorService
                .bindEntity(
                    true,
                    replacement,
                    "probe",
                    0,
                    null,
                    Collections.<String>emptyList(),
                    null,
                    snapshot,
                    identities,
                    selections)
                .isAccepted() && selections.get(replacement) == null
                && identities.getHost("hero") == null,
            "explicit empty unbind");
        selections.put(new NominatorEntityBinding(entity, null, Arrays.asList("townfolk"), "kingdom"));
        NBTTagCompound saved = new NBTTagCompound();
        selections.writeToNBT(saved);
        NominatorSavedData restarted = new NominatorSavedData("probe_nominator_restart");
        restarted.readFromNBT(saved);
        require(
            restarted.get(entity) != null && restarted.get(entity)
                .getGroupIds()
                .equals(Arrays.asList("townfolk")) && restarted.getRevision() == selections.getRevision(),
            "selection persistence");
        C2SNominatorEntityBind entityPacket = new C2SNominatorEntityBind(
            4,
            entity,
            null,
            Arrays.asList("townfolk"),
            "kingdom",
            12L,
            true);
        ByteBuf entityBuffer = Unpooled.buffer();
        entityPacket.toBytes(entityBuffer);
        C2SNominatorEntityBind decodedEntityPacket = new C2SNominatorEntityBind();
        decodedEntityPacket.fromBytes(entityBuffer);
        require(
            decodedEntityPacket.isTransfer() && decodedEntityPacket.getExpectedRevision() == 12L
                && decodedEntityPacket.getExpectedRevision() != selections.getRevision(),
            "entity codec stale fence");
        S2CNominatorEntityOpen openPacket = new S2CNominatorEntityOpen(
            4,
            entity,
            12L,
            "hero",
            Arrays.asList("townfolk"),
            "kingdom");
        ByteBuf openBuffer = Unpooled.buffer();
        openPacket.toBytes(openBuffer);
        S2CNominatorEntityOpen decodedOpenPacket = new S2CNominatorEntityOpen();
        decodedOpenPacket.fromBytes(openBuffer);
        require(
            decodedOpenPacket.getRevision() == 12L && decodedOpenPacket.getGroups()
                .size() == 1,
            "open codec");
        S2CNominatorEntityOpen catalogPacket = new S2CNominatorEntityOpen(
            4,
            entity,
            12L,
            "Zombie",
            "minecraft:zombie",
            "hero",
            Arrays.asList("townfolk"),
            Arrays.asList("townfolk"),
            "kingdom",
            NominatorCatalog.from(snapshot));
        ByteBuf catalogBuffer = Unpooled.buffer();
        catalogPacket.toBytes(catalogBuffer);
        S2CNominatorEntityOpen decodedCatalog = new S2CNominatorEntityOpen();
        decodedCatalog.fromBytes(catalogBuffer);
        require(
            "Zombie".equals(decodedCatalog.getDisplayName())
                && "minecraft:zombie".equals(decodedCatalog.getEntityType())
                && decodedCatalog.getCatalog()
                    .getActors()
                    .size() == 2
                && decodedCatalog.getTypeGroups()
                    .size() == 1,
            "server catalog codec");
        ByteBuf trailing = Unpooled.buffer();
        catalogPacket.toBytes(trailing);
        trailing.writeByte(1);
        boolean rejectedTrailing = false;
        try {
            new S2CNominatorEntityOpen().fromBytes(trailing);
        } catch (IllegalArgumentException expected) {
            rejectedTrailing = true;
        }
        require(rejectedTrailing, "catalog trailing bytes");
        require(
            !NominatorService
                .bindInventory(
                    true,
                    null,
                    "item",
                    null,
                    Collections.<String>emptyList(),
                    snapshot,
                    new ItemIdentitySavedData("probe_item"))
                .isAccepted(),
            "empty selected stack");
        require(
            NominatorService.bindEntityTypeGroup(true, "minecraft:zombie", "townfolk", true, snapshot, selections)
                .isAccepted()
                && selections.getTypeGroups("minecraft:zombie")
                    .equals(Arrays.asList("townfolk")),
            "exact type group");
        require(
            !NominatorService.bindEntityTypeGroup(true, "customnpcs:customnpc", "townfolk", true, snapshot, selections)
                .isAccepted(),
            "wrapper type is fail closed");
        NBTTagCompound typeCheckpoint = new NBTTagCompound();
        selections.writeToNBT(typeCheckpoint);
        NominatorSavedData typeRestart = new NominatorSavedData("probe_type_restart");
        typeRestart.readFromNBT(typeCheckpoint);
        require(
            typeRestart.getTypeGroups("minecraft:zombie")
                .equals(Arrays.asList("townfolk")),
            "type group persistence");
        C2SNominatorInventoryBind request = new C2SNominatorInventoryBind(
            5,
            "item",
            null,
            Arrays.asList("group_a", "group_b"));
        ByteBuf buffer = Unpooled.buffer();
        request.toBytes(buffer);
        C2SNominatorInventoryBind decoded = new C2SNominatorInventoryBind();
        decoded.fromBytes(buffer);
        require(
            decoded.getSelectedSlot() == 5 && decoded.getFuzzyGroups()
                .size() == 2,
            "selected slot packet");
        boolean rejectedSlot = false;
        try {
            new C2SNominatorInventoryBind(36, "item", null, Collections.<String>emptyList()).toBytes(Unpooled.buffer());
        } catch (IllegalArgumentException expected) {
            rejectedSlot = true;
        }
        require(rejectedSlot, "spoofed slot");
        System.out.println(
            "NOMINATOR_STAGE4_PROBE=PASS search permissions conflict individual multi-group selected-slot empty-spoof type-group catalog");
    }

    private static void require(boolean value, String label) {
        if (!value) throw new IllegalStateException("Probe failed: " + label);
    }
}
