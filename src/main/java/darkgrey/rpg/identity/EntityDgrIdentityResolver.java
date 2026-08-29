package darkgrey.rpg.identity;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityList;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;

/** Resolves interaction identities without making CustomNPC+ a runtime dependency. */
public final class EntityDgrIdentityResolver {

    public enum Source {
        NPC_IDENTITY,
        NOMINATOR_INDIVIDUAL,
        NOMINATOR_GROUP,
        LEGACY_CUSTOMNPC,
        NONE
    }

    private EntityDgrIdentityResolver() {}

    /** Resolves against server-owned external registries, then legacy CNPC data. */
    public static Resolution resolve(Entity entity) {
        if (entity == null) return Resolution.none();
        NpcIdentitySavedData identities = null;
        NominatorSavedData selections = null;
        try {
            identities = NpcIdentitySavedData.get();
            selections = NominatorSavedData.get();
        } catch (RuntimeException ignored) {
            // Offline/client probes have no server SavedData; legacy fallback remains safe.
        }
        return resolve(entity, identities, selections);
    }

    /** Deterministic/testable seam; callers own the supplied SavedData instances. */
    public static Resolution resolve(Entity entity, NpcIdentitySavedData identities, NominatorSavedData selections) {
        return resolve(entity, entity == null ? null : EntityList.getEntityString(entity), identities, selections);
    }

    /** Test/compatibility seam when the authoritative registry type is already known. */
    public static Resolution resolve(Entity entity, String entityType, NpcIdentitySavedData identities,
        NominatorSavedData selections) {
        if (entity == null) return Resolution.none();
        UUID uuid = entity.getUniqueID();
        if (uuid == null) return Resolution.none();
        String primaryId = null;
        Source primarySource = Source.NONE;
        if (identities != null) {
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
                if (primaryId == null && !blank(binding.getIndividualId())) {
                    primaryId = binding.getIndividualId()
                        .trim();
                    primarySource = Source.NOMINATOR_INDIVIDUAL;
                    resolved.add(primaryId);
                }
                for (String group : binding.getGroupIds()) addIfPresent(resolved, group);
            }
            if (darkgrey.rpg.nominator.NominatorService.safeType(entityType))
                for (String group : selections.getTypeGroups(entityType)) addIfPresent(resolved, group);
        }
        if (!resolved.isEmpty()) return new Resolution(
            primarySource == Source.NONE ? Source.NOMINATOR_GROUP : primarySource,
            new ArrayList<String>(resolved));
        try {
            String legacy = CustomNpcActorBinding.getActorId(entity);
            if (!blank(legacy)) return Resolution.single(Source.LEGACY_CUSTOMNPC, legacy);
        } catch (RuntimeException ignored) {
            // Optional bridge failures must never abort Forge event dispatch.
        } catch (LinkageError ignored) {
            // Optional bridge failures must never abort Forge event dispatch.
        }
        return Resolution.none();
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

        private static Resolution single(Source source, String actorId) {
            return new Resolution(source, Collections.singletonList(actorId.trim()));
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
