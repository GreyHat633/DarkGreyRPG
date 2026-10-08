package darkgrey.rpg.nominator;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.message.nominator.S2CNominatorEntityOpen;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Focused Stage 4 probe: search, permission, individual/group validation, and conflict handling. */
public final class NominatorStage4Probe {

    private static final String STORY = "ST-2345-6789-ABCD-EFGH";
    private static final String HERO = actor("hero");
    private static final String TOWN = actor("townfolk");

    private static String actor(String local) {
        return new darkgrey.rpg.identity.ResourceAddress(
            darkgrey.rpg.identity.StoryUid.parse(STORY),
            darkgrey.rpg.identity.ResourceAddress.Kind.ACTOR,
            local).toKey();
    }

    private NominatorStage4Probe() {}

    public static void main(String[] args) {
        LinkedHashMap<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            HERO,
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_INDIVIDUAL,
                HERO,
                "Hero",
                "",
                Collections.<String>emptyList(),
                STORY));
        actors.put(
            TOWN,
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_COLLECTIVE,
                TOWN,
                "Townfolk",
                "",
                Collections.<String>emptyList(),
                STORY));
        LinkedHashMap<String, CanonicalGraphResource> canonicalStories = new LinkedHashMap<String, CanonicalGraphResource>();
        canonicalStories.put(
            STORY,
            new CanonicalGraphResource(
                CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
                CanonicalGraphResourceKind.STORY,
                STORY,
                "Canonical Kingdom",
                new CanonicalGraph(Collections.emptyList(), Collections.emptyList())));
        ProjectSnapshot canonicalOnly = new ProjectSnapshot(
            new ProjectDefinition(1, "canonical_probe", "Canonical Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                canonicalStories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.emptyMap()));
        ProjectSnapshot snapshot = canonicalOnly;
        require(canonicalOnly.containsStory(STORY), "canonical-only story loaded state");
        require(!canonicalOnly.containsStory("missing"), "missing story loaded state");
        require(
            NominatorCatalog.from(canonicalOnly)
                .getStories()
                .size() == 1,
            "canonical-only story catalog");
        require(
            NominatorService
                .bindEntity(
                    true,
                    UUID.randomUUID(),
                    "probe",
                    0,
                    HERO,
                    Collections.<String>emptyList(),
                    STORY,
                    canonicalOnly,
                    new NpcIdentitySavedData("canonical_only_npc"),
                    new NominatorSavedData())
                .isAccepted(),
            "canonical-only story binding");
        NominatorCatalog catalog = NominatorCatalog.from(snapshot);
        NominatorStorySearch.ActorChoice exactIndividual = NominatorStorySearch.exactActor(catalog, HERO);
        require(
            exactIndividual != null && HERO.equals(exactIndividual.getId())
                && ActorDefinition.TYPE_INDIVIDUAL.equals(exactIndividual.getType()),
            "exact individual actor resolution");
        NominatorStorySearch.ActorChoice exactCollective = NominatorStorySearch.exactActor(catalog, TOWN);
        require(
            exactCollective != null && TOWN.equals(exactCollective.getId())
                && ActorDefinition.TYPE_COLLECTIVE.equals(exactCollective.getType()),
            "exact collective actor resolution");
        require(NominatorStorySearch.exactActor(catalog, "missing") == null, "unknown actor resolution");
        require(NominatorStorySearch.exactActor(catalog, "her") == null, "partial actor resolution");
        require(
            NominatorStorySearch.actors(snapshot, STORY, "Hero")
                .size() == 1,
            "story search");
        NpcIdentitySavedData identities = new NpcIdentitySavedData("probe_npc");
        NominatorSavedData selections = new NominatorSavedData();
        UUID entity = UUID.randomUUID();
        require(
            !NominatorService
                .bindEntity(
                    false,
                    entity,
                    "probe",
                    0,
                    HERO,
                    Collections.<String>emptyList(),
                    STORY,
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
                    HERO,
                    Arrays.asList(TOWN),
                    STORY,
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
                    actor("other"),
                    Collections.<String>emptyList(),
                    STORY,
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
                    HERO,
                    Collections.<String>emptyList(),
                    STORY,
                    true,
                    snapshot,
                    identities,
                    selections)
                .isAccepted()
                && identities.getHost(HERO)
                    .getEntityUuid()
                    .equals(replacement)
                && selections.get(entity)
                    .getIndividualId() == null
                && selections.get(entity)
                    .getGroupIds()
                    .equals(Arrays.asList(TOWN)),
            "explicit transfer clears old individual");
        require(
            NominatorService
                .bindEntity(
                    true,
                    entity,
                    "probe",
                    0,
                    null,
                    Arrays.asList(TOWN),
                    STORY,
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
                && identities.getHost(HERO) == null,
            "explicit empty unbind");
        selections.put(new NominatorEntityBinding(entity, null, Arrays.asList(TOWN), STORY));
        NBTTagCompound saved = new NBTTagCompound();
        selections.writeToNBT(saved);
        NominatorSavedData restarted = new NominatorSavedData("probe_nominator_restart");
        restarted.readFromNBT(saved);
        require(
            restarted.get(entity) != null && restarted.get(entity)
                .getGroupIds()
                .equals(Arrays.asList(TOWN)) && restarted.getRevision() == selections.getRevision(),
            "selection persistence");
        S2CNominatorEntityOpen openPacket = new S2CNominatorEntityOpen(
            4,
            entity,
            12L,
            HERO,
            Arrays.asList(TOWN),
            STORY);
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
            34L,
            "Zombie",
            "minecraft:zombie",
            HERO,
            Arrays.asList(TOWN),
            STORY,
            catalog);
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
                && decodedCatalog.getCatalogRevision() == 34L,
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
        System.out.println(
            "NOMINATOR_STAGE4_PROBE=PASS search permissions conflict transfer clear individual multi-group persistence current-open-codec catalog");
    }

    private static void require(boolean value, String label) {
        if (!value) throw new IllegalStateException("Probe failed: " + label);
    }
}
