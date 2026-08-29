package darkgrey.rpg.entitytools;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

/** Sanitized, UUID-free entity template. NPC identity is display metadata only. */
public final class EntityTemplate {

    private final String entityType;
    private final NBTTagCompound configuration;
    private final NBTTagCompound appearance;
    private final NBTTagCompound attributes;
    private final NBTTagCompound equipment;
    private final NBTTagCompound ai;
    private final NBTTagCompound extra;
    private final String sourceNpcId;
    private final List<String> groups;

    public EntityTemplate(String entityType, NBTTagCompound configuration, NBTTagCompound appearance,
        NBTTagCompound attributes, NBTTagCompound equipment, NBTTagCompound ai, NBTTagCompound extra,
        String sourceNpcId, List<String> groups) {
        if (blank(entityType)) throw new IllegalArgumentException("Entity type is required.");
        this.entityType = entityType.trim();
        this.configuration = EntityTemplateSanitizer.sanitize(configuration);
        this.appearance = EntityTemplateSanitizer.sanitize(appearance);
        this.attributes = EntityTemplateSanitizer.sanitize(attributes);
        this.equipment = EntityTemplateSanitizer.sanitize(equipment);
        this.ai = EntityTemplateSanitizer.sanitize(ai);
        this.extra = EntityTemplateSanitizer.sanitize(extra);
        this.sourceNpcId = sourceNpcId == null || sourceNpcId.trim()
            .isEmpty() ? null : sourceNpcId.trim();
        this.groups = normalizeGroups(groups);
    }

    public static EntityTemplate fromCapture(EntityCapture capture) {
        if (capture == null) throw new IllegalArgumentException("Entity capture is required.");
        return new EntityTemplate(
            capture.getEntityType(),
            capture.getConfiguration(),
            capture.getAppearance(),
            capture.getAttributes(),
            capture.getEquipment(),
            capture.getAi(),
            capture.getExtra(),
            capture.getSourceNpcId(),
            capture.getGroups());
    }

    public String getEntityType() {
        return entityType;
    }

    public NBTTagCompound getConfiguration() {
        return copy(configuration);
    }

    public NBTTagCompound getAppearance() {
        return copy(appearance);
    }

    public NBTTagCompound getAttributes() {
        return copy(attributes);
    }

    public NBTTagCompound getEquipment() {
        return copy(equipment);
    }

    public NBTTagCompound getAi() {
        return copy(ai);
    }

    public NBTTagCompound getExtra() {
        return copy(extra);
    }

    public String getSourceNpcId() {
        return sourceNpcId;
    }

    public List<String> getGroups() {
        return groups;
    }

    /** Creates a fresh-UUID spawn request; no position, dimension, or NPC ID is included. */
    public EntitySpawnSpec spawnFresh() {
        return spawn(UUID.randomUUID(), null);
    }

    /** Creates a spawn request for an explicit UUID, intended only for survival restoration. */
    public EntitySpawnSpec spawn(UUID uuid, String reservedNpcId) {
        if (uuid == null) throw new IllegalArgumentException("Spawn UUID is required.");
        return new EntitySpawnSpec(
            uuid,
            entityType,
            configuration,
            appearance,
            attributes,
            equipment,
            ai,
            extra,
            groups,
            reservedNpcId);
    }

    static List<String> normalizeGroups(List<String> values) {
        Set<String> unique = new HashSet<String>();
        if (values != null) {
            for (String value : values) {
                if (value == null || value.trim()
                    .isEmpty()) throw new IllegalArgumentException("Group ID cannot be blank.");
                unique.add(value.trim());
            }
        }
        List<String> result = new ArrayList<String>(unique);
        Collections.sort(result);
        return Collections.unmodifiableList(result);
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return (NBTTagCompound) value.copy();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
