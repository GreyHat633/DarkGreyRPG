package darkgrey.rpg.identity;

import java.lang.reflect.Field;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;

import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;
import sun.misc.Unsafe;

/** Focused offline proof of external identity precedence and group routing. */
public final class EntityDgrIdentityResolverProbe {

    private static final UUID ENTITY = UUID.fromString("00000000-0000-0000-0000-000000000401");

    private EntityDgrIdentityResolverProbe() {}

    public static void main(String[] args) throws Exception {
        Entity entity = allocateEntity(ENTITY);
        NpcIdentitySavedData identities = new NpcIdentitySavedData("probe_identity_resolver");
        NominatorSavedData selections = new NominatorSavedData("probe_identity_resolver_nominator");

        EntityDgrIdentityResolver.Resolution unboundSlime = EntityDgrIdentityResolver
            .resolve(entity, "minecraft:slime", identities, selections);
        require(!unboundSlime.isResolved(), "unbound vanilla slime acquired a DGR identity");

        selections.put(new NominatorEntityBinding(ENTITY, null, Collections.singletonList("slimes"), "kill_slimes"));
        for (String hostType : Arrays.asList("minecraft:cow", "minecraft:pig", "minecraft:zombie")) {
            EntityDgrIdentityResolver.Resolution nominated = EntityDgrIdentityResolver
                .resolve(entity, hostType, identities, selections);
            require(
                nominated.getActorIds()
                    .equals(Collections.singletonList("slimes")),
                hostType + " did not resolve the nominated DGR group");
        }

        selections.put(new NominatorEntityBinding(ENTITY, null, Arrays.asList("guards", "town", "guards"), null));
        EntityDgrIdentityResolver.Resolution groups = EntityDgrIdentityResolver.resolve(entity, identities, selections);
        require(groups.isExternal(), "group selection was not external");
        require(
            groups.getActorIds()
                .size() == 2,
            "all groups were not retained");
        require("guards".equals(groups.getActorId()), "first group ordering changed");
        selections.addTypeGroup("minecraft:zombie", "type_only");
        EntityDgrIdentityResolver.Resolution typed = EntityDgrIdentityResolver
            .resolve(entity, "minecraft:zombie", identities, selections);
        require(
            typed.getActorIds()
                .size() == 3 && typed.getActorIds()
                    .contains("type_only"),
            "exact type group routing");

        selections.put(new NominatorEntityBinding(ENTITY, "stale_individual", Arrays.asList("guards", "town"), null));
        EntityDgrIdentityResolver.Resolution nominator = EntityDgrIdentityResolver
            .resolve(entity, identities, selections);
        require(
            nominator.getActorIds()
                .size() == 3,
            "nominator individual and groups were not combined");
        require(
            "stale_individual".equals(
                nominator.getActorIds()
                    .get(0)),
            "nominator individual was not first");

        identities.bind("tavern_boss", new NpcHostIdentity(ENTITY, "customnpcs:customnpc", 0));
        selections.put(
            new NominatorEntityBinding(
                ENTITY,
                "stale_individual",
                Arrays.asList("guards", "tavern_boss", "town", "guards"),
                null));
        EntityDgrIdentityResolver.Resolution npc = EntityDgrIdentityResolver.resolve(entity, identities, selections);
        require(npc.getSource() == EntityDgrIdentityResolver.Source.NPC_IDENTITY, "NPC identity did not win");
        require("tavern_boss".equals(npc.getActorId()), "NPC identity mismatch");
        List<String> resolved = npc.getActorIds();
        require(resolved.size() == 3, "registry identity did not retain stable deduped groups");
        require("guards".equals(resolved.get(1)) && "town".equals(resolved.get(2)), "registry/group ordering changed");
        require(!resolved.contains("stale_individual"), "stale nominator individual leaked through registry");
        require(
            EntityDgrIdentityResolver.resolve(null, identities, selections)
                .getActorId() == null,
            "null entity resolved unexpectedly");
        Entity original = allocateEntity(ENTITY);
        original.setEntityId(40);
        Entity clone = allocateEntity(ENTITY);
        clone.setEntityId(41);
        List<Entity> duplicates = Arrays.asList(original, clone);
        require(
            EntityDgrIdentityResolver.isCanonicalUuidHost(original, duplicates),
            "original same-UUID host was rejected");
        require(
            !EntityDgrIdentityResolver.isCanonicalUuidHost(clone, duplicates),
            "later same-UUID clone retained unique-host eligibility");
        require(
            EntityDgrIdentityResolver.isCanonicalUuidHost(original, Collections.singletonList(original)),
            "single host was rejected");
        System.out.println("DGR_IDENTITY_EXTERNAL_PRECEDENCE=PASS");
        System.out.println("DGR_IDENTITY_NOMINATOR_COMBINATION=PASS");
        System.out.println("DGR_IDENTITY_STALE_INDIVIDUAL_REJECTED=PASS");
        System.out.println("DGR_IDENTITY_GROUP_DEDUP=PASS");
        System.out.println("DGR_IDENTITY_GROUP_ROUTING=PASS");
        System.out.println("DGR_IDENTITY_EXACT_TYPE_GROUP_ROUTING=PASS");
        System.out.println("DGR_IDENTITY_SAME_UUID_CLONE_GUARD=PASS");
        System.out.println("DGR_B2_UNBOUND_SLIME_NEGATIVE=PASS");
        System.out.println("DGR_B2_MULTI_HOST_SLIMES_GROUP=PASS");
    }

    private static Entity allocateEntity(UUID uuid) throws Exception {
        Field unsafeField = Unsafe.class.getDeclaredField("theUnsafe");
        unsafeField.setAccessible(true);
        Unsafe unsafe = (Unsafe) unsafeField.get(null);
        Entity entity = (Entity) unsafe.allocateInstance(ProbeEntity.class);
        Field id = Entity.class.getDeclaredField("entityUniqueID");
        id.setAccessible(true);
        id.set(entity, uuid);
        return entity;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static final class ProbeEntity extends Entity {

        private ProbeEntity() {
            super(null);
        }

        @Override
        protected void entityInit() {}

        @Override
        protected void readEntityFromNBT(net.minecraft.nbt.NBTTagCompound value) {}

        @Override
        protected void writeEntityToNBT(net.minecraft.nbt.NBTTagCompound value) {}
    }
}
