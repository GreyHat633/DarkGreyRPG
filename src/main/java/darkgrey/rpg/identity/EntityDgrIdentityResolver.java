package darkgrey.rpg.identity;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;

/** Resolves interaction identities without making CustomNPC+ a runtime dependency. */
public final class EntityDgrIdentityResolver {

    public enum Source {
        NPC_IDENTITY,
        NOMINATOR_INDIVIDUAL,
        NOMINATOR_GROUP,
        NONE
    }

    private EntityDgrIdentityResolver() {}

    /** Resolves exclusively against server-owned external identity registries. */
    public static Resolution resolve(Entity entity) {
        if (entity == null) return Resolution.none();
        NpcIdentitySavedData identities = null;
        NominatorSavedData selections = null;
        try {
            identities = NpcIdentitySavedData.get();
            selections = NominatorSavedData.get();
        } catch (RuntimeException ignored) {
            // No partial identity result when either server-owned registry cannot be read.
            return Resolution.none();
        }
        return resolve(entity, identities, selections);
    }

    /** Deterministic/testable seam; callers own the supplied SavedData instances. */
    public static Resolution resolve(Entity entity, NpcIdentitySavedData identities, NominatorSavedData selections) {
        if (entity == null) return Resolution.none();
        if (selections != null) selections.requireUsable();
        UUID uuid = entity.getUniqueID();
        if (uuid == null) return Resolution.none();
        boolean canonicalUniqueHost = isCanonicalUniqueHost(entity);
        String primaryId = null;
        Source primarySource = Source.NONE;
        if (identities != null && canonicalUniqueHost) {
            String npcId = identities.getNpcId(uuid);
            if (!blank(npcId)) {
                primaryId = npcId.trim();
                primarySource = Source.NPC_IDENTITY;
            }
        }
        LinkedHashSet<String> resolved = new LinkedHashSet<String>();
        if (primaryId != null) resolved.add(primaryId);
        if (selections != null) {
            NominatorEntityBinding binding = selections.get(uuid);
            if (binding != null) {
                // A registry identity is authoritative over a stale nominator
                // individual, but never suppresses the selected groups.
                if (canonicalUniqueHost && primaryId == null && !blank(binding.getIndividualId())) {
                    primaryId = binding.getIndividualId()
                        .trim();
                    primarySource = Source.NOMINATOR_INDIVIDUAL;
                    resolved.add(primaryId);
                }
                for (String group : binding.getGroupIds()) addIfPresent(resolved, group);
            }
        }
        if (!resolved.isEmpty()) return new Resolution(
            primarySource == Source.NONE ? Source.NOMINATOR_GROUP : primarySource,
            new ArrayList<String>(resolved));
        return Resolution.none();
    }

    /**
     * CustomNPC+ Cloner preserves the source UUID. The first live instance is
     * the already-existing host; later same-UUID instances may retain groups
     * but must never resolve the unique individual identity.
     */
    private static boolean isCanonicalUniqueHost(Entity entity) {
        if (!CustomNpcActorBinding.isCustomNpc(entity) || entity.worldObj == null) return true;
        return isCanonicalUuidHost(entity, entity.worldObj.loadedEntityList);
    }

    static boolean isCanonicalUuidHost(Entity entity, Iterable<?> loadedEntities) {
        if (entity == null || entity.getUniqueID() == null || loadedEntities == null) return true;
        int canonicalEntityId = entity.getEntityId();
        for (Object value : loadedEntities) {
            if (!(value instanceof Entity)) continue;
            Entity candidate = (Entity) value;
            if (candidate == entity || !entity.getUniqueID()
                .equals(candidate.getUniqueID())) continue;
            if (candidate.getEntityId() < canonicalEntityId) return false;
        }
        return true;
    }

    private static void addIfPresent(LinkedHashSet<String> values, String value) {
        if (!blank(value)) values.add(value.trim());
    }

    public static String resolveActorId(Entity entity) {
        return resolve(entity).getActorId();
    }

    public static String resolveActorId(Entity entity, NpcIdentitySavedData identities, NominatorSavedData selections) {
        return resolve(entity, identities, selections).getActorId();
    }

    public static List<String> resolveActorIds(Entity entity) {
        return resolve(entity).getActorIds();
    }

    public static List<String> resolveActorIds(Entity entity, NpcIdentitySavedData identities,
        NominatorSavedData selections) {
        return resolve(entity, identities, selections).getActorIds();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    public static final class Resolution {

        private final Source source;
        private final List<String> actorIds;

        private Resolution(Source source, List<String> actorIds) {
            this.source = source;
            this.actorIds = Collections.unmodifiableList(new ArrayList<String>(actorIds));
        }

        private static Resolution none() {
            return new Resolution(Source.NONE, Collections.<String>emptyList());
        }

        public Source getSource() {
            return source;
        }

        public List<String> getActorIds() {
            return actorIds;
        }

        public String getActorId() {
            return actorIds.isEmpty() ? null : actorIds.get(0);
        }

        public boolean isResolved() {
            return !actorIds.isEmpty();
        }

        public boolean isExternal() {
            return source == Source.NPC_IDENTITY || source == Source.NOMINATOR_INDIVIDUAL
                || source == Source.NOMINATOR_GROUP;
        }
    }
}
