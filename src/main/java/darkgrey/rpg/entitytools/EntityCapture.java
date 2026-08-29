package darkgrey.rpg.entitytools;

import java.util.List;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

/** Immutable server-side description of an entity supplied by an integration adapter. */
public final class EntityCapture {

    private final UUID entityUuid;
    private final String entityType;
    private final int dimension;
    private final boolean living;
    private final boolean player;
    private final NBTTagCompound configuration;
    private final NBTTagCompound appearance;
    private final NBTTagCompound attributes;
    private final NBTTagCompound equipment;
    private final NBTTagCompound ai;
    private final NBTTagCompound extra;
    private final String sourceNpcId;
    private final List<String> groups;

    public EntityCapture(UUID entityUuid, String entityType, int dimension, boolean living, boolean player,
        NBTTagCompound configuration, NBTTagCompound appearance, NBTTagCompound attributes, NBTTagCompound equipment,
        NBTTagCompound ai, NBTTagCompound extra, String sourceNpcId, List<String> groups) {
        if (entityUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        if (blank(entityType)) throw new IllegalArgumentException("Entity type is required.");
        if (player && !living) throw new IllegalArgumentException("A player must be living.");
        this.entityUuid = entityUuid;
        this.entityType = entityType.trim();
        this.dimension = dimension;
        this.living = living;
        this.player = player;
        this.configuration = copy(configuration);
        this.appearance = copy(appearance);
        this.attributes = copy(attributes);
        this.equipment = copy(equipment);
        this.ai = copy(ai);
        this.extra = copy(extra);
        this.sourceNpcId = normalizedOptional(sourceNpcId);
        this.groups = EntityTemplate.normalizeGroups(groups);
    }

    public UUID getEntityUuid() {
        return entityUuid;
    }

    public String getEntityType() {
        return entityType;
    }

    public int getDimension() {
        return dimension;
    }

    public boolean isLiving() {
        return living;
    }

    public boolean isPlayer() {
        return player;
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

    public static void requireLivingMob(EntityCapture capture) {
        if (capture == null) throw new IllegalArgumentException("Entity capture is required.");
        if (!capture.isLiving() || capture.isPlayer())
            throw new IllegalArgumentException("Only living non-player mobs can be captured.");
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return value == null ? new NBTTagCompound() : (NBTTagCompound) value.copy();
    }

    private static String normalizedOptional(String value) {
        if (value == null || value.trim()
            .isEmpty()) return null;
        return value.trim();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
