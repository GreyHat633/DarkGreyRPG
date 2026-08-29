package darkgrey.rpg.entitytools;

import java.util.List;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

/** Explicit output passed by the core to a Forge entity-spawn adapter. */
public final class EntitySpawnSpec {

    private final UUID uuid;
    private final String entityType;
    private final NBTTagCompound configuration;
    private final NBTTagCompound appearance;
    private final NBTTagCompound attributes;
    private final NBTTagCompound equipment;
    private final NBTTagCompound ai;
    private final NBTTagCompound extra;
    private final java.util.List<String> groups;
    private final String reservedNpcId;

    EntitySpawnSpec(UUID uuid, String entityType, NBTTagCompound configuration, NBTTagCompound appearance,
        NBTTagCompound attributes, NBTTagCompound equipment, NBTTagCompound ai, NBTTagCompound extra,
        List<String> groups, String reservedNpcId) {
        this.uuid = uuid;
        this.entityType = entityType;
        this.configuration = (NBTTagCompound) configuration.copy();
        this.appearance = (NBTTagCompound) appearance.copy();
        this.attributes = (NBTTagCompound) attributes.copy();
        this.equipment = (NBTTagCompound) equipment.copy();
        this.ai = (NBTTagCompound) ai.copy();
        this.extra = (NBTTagCompound) extra.copy();
        this.groups = EntityTemplate.normalizeGroups(groups);
        this.reservedNpcId = reservedNpcId == null || reservedNpcId.trim()
            .isEmpty() ? null : reservedNpcId.trim();
    }

    public UUID getUuid() {
        return uuid;
    }

    public UUID getEntityUuid() {
        return uuid;
    }

    public String getEntityType() {
        return entityType;
    }

    public NBTTagCompound getConfiguration() {
        return (NBTTagCompound) configuration.copy();
    }

    public NBTTagCompound getAppearance() {
        return (NBTTagCompound) appearance.copy();
    }

    public NBTTagCompound getAttributes() {
        return (NBTTagCompound) attributes.copy();
    }

    public NBTTagCompound getEquipment() {
        return (NBTTagCompound) equipment.copy();
    }

    public NBTTagCompound getAi() {
        return (NBTTagCompound) ai.copy();
    }

    public NBTTagCompound getExtra() {
        return (NBTTagCompound) extra.copy();
    }

    public List<String> getGroups() {
        return groups;
    }

    public String getReservedNpcId() {
        return reservedNpcId;
    }

    public boolean hasEffectiveNpcId() {
        return reservedNpcId != null;
    }
}
