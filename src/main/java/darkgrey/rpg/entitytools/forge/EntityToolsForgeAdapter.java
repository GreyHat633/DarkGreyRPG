package darkgrey.rpg.entitytools.forge;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityList;
import net.minecraft.entity.EntityLivingBase;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.nbt.NBTBase;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagDouble;
import net.minecraft.nbt.NBTTagFloat;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.World;

import darkgrey.rpg.entitytools.EntityCapture;
import darkgrey.rpg.entitytools.EntitySpawnSpec;
import darkgrey.rpg.identity.NpcHostIdentity;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;

/** Converts real Forge entities to/from the identity-free Stage 5 core model. */
public final class EntityToolsForgeAdapter {

    public EntityCapture capture(Entity entity) {
        if (!(entity instanceof EntityLivingBase) || entity instanceof EntityPlayer)
            throw new IllegalArgumentException("Only living non-player mobs can be captured.");
        String entityType = EntityList.getEntityString(entity);
        if (entityType == null || entityType.trim()
            .isEmpty())
            throw new IllegalArgumentException("Entity type is not registered and cannot be restored safely.");

        NBTTagCompound full = new NBTTagCompound();
        if (!entity.writeToNBTOptional(full))
            throw new IllegalArgumentException("Entity refused serialization and cannot be captured safely.");
        UUID uuid = entity.getUniqueID();
        NominatorEntityBinding binding = NominatorSavedData.get()
            .get(uuid);
        List<String> groups = binding == null ? new ArrayList<String>() : binding.getGroupIds();
        String npcId = NpcIdentitySavedData.get()
            .getNpcId(uuid);
        return new EntityCapture(
            uuid,
            entityType,
            entity.dimension,
            true,
            false,
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            full,
            npcId,
            groups);
    }

    /** Spawns first, then publishes external DGR identity/group state; failures remove the candidate. */
    public Entity spawn(World world, double x, double y, double z, EntitySpawnSpec spec) {
        if (world == null || world.isRemote) throw new IllegalArgumentException("Server World is required.");
        if (spec == null) throw new IllegalArgumentException("Entity spawn specification is required.");
        NBTTagCompound root = merge(spec);
        putLocation(root, x, y, z, spec.getUuid());
        Entity entity = EntityList.createEntityFromNBT(root, world);
        if (!(entity instanceof EntityLivingBase) || entity instanceof EntityPlayer)
            throw new IllegalArgumentException("Stored entity no longer resolves to a living non-player mob.");
        entity.setLocationAndAngles(x, y, z, entity.rotationYaw, entity.rotationPitch);
        if (!world.spawnEntityInWorld(entity)) throw new IllegalStateException("World rejected the restored entity.");
        try {
            publishExternalState(entity, spec);
        } catch (RuntimeException exception) {
            entity.setDead();
            throw exception;
        }
        return entity;
    }

    private static void publishExternalState(Entity entity, EntitySpawnSpec spec) {
        List<String> groups = spec.getGroups();
        String npcId = spec.getReservedNpcId();
        if (npcId != null) {
            NpcIdentitySavedData identities = NpcIdentitySavedData.get();
            NpcHostIdentity current = identities.getHost(npcId);
            if (current == null || !current.getEntityUuid()
                .equals(entity.getUniqueID()))
                throw new IllegalStateException(
                    "Reserved NPC identity is no longer held by the stored entity; explicit Nominator transfer is required.");
            NpcHostIdentity observed = new NpcHostIdentity(
                entity.getUniqueID(),
                spec.getEntityType(),
                entity.dimension,
                current.getCompatibilityKey());
            identities.observe(observed);
        }
        if (npcId != null || !groups.isEmpty()) NominatorSavedData.get()
            .put(new NominatorEntityBinding(entity.getUniqueID(), npcId, groups, null));
    }

    private static NBTTagCompound merge(EntitySpawnSpec spec) {
        NBTTagCompound root = spec.getExtra();
        overlay(root, spec.getConfiguration());
        overlay(root, spec.getAppearance());
        overlay(root, spec.getAttributes());
        overlay(root, spec.getEquipment());
        overlay(root, spec.getAi());
        root.setString("id", spec.getEntityType());
        return root;
    }

    private static void overlay(NBTTagCompound target, NBTTagCompound source) {
        for (Object keyValue : source.func_150296_c()) {
            String key = String.valueOf(keyValue);
            NBTBase value = source.getTag(key);
            if (value != null) target.setTag(key, value.copy());
        }
    }

    private static void putLocation(NBTTagCompound root, double x, double y, double z, UUID uuid) {
        NBTTagList pos = new NBTTagList();
        pos.appendTag(new NBTTagDouble(x));
        pos.appendTag(new NBTTagDouble(y));
        pos.appendTag(new NBTTagDouble(z));
        root.setTag("Pos", pos);
        NBTTagList motion = new NBTTagList();
        motion.appendTag(new NBTTagDouble(0.0D));
        motion.appendTag(new NBTTagDouble(0.0D));
        motion.appendTag(new NBTTagDouble(0.0D));
        root.setTag("Motion", motion);
        NBTTagList rotation = new NBTTagList();
        rotation.appendTag(new NBTTagFloat(0.0F));
        rotation.appendTag(new NBTTagFloat(0.0F));
        root.setTag("Rotation", rotation);
        root.setLong("UUIDMost", uuid.getMostSignificantBits());
        root.setLong("UUIDLeast", uuid.getLeastSignificantBits());
    }
}
