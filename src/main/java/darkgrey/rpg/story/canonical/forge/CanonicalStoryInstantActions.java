package darkgrey.rpg.story.canonical.forge;

import net.minecraft.command.ICommandSender;
import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.potion.Potion;
import net.minecraft.potion.PotionEffect;
import net.minecraft.server.MinecraftServer;
import net.minecraft.util.ChunkCoordinates;
import net.minecraft.util.IChatComponent;
import net.minecraft.world.Teleporter;
import net.minecraft.world.World;
import net.minecraft.world.WorldServer;
import net.minecraftforge.common.DimensionManager;

import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;

/** Instant server-installed Story commands; no client command transport. */
public final class CanonicalStoryInstantActions {

    private CanonicalStoryInstantActions() {}

    public static boolean apply(final EntityPlayerMP player, final CanonicalStoryActionConfiguration configuration) {
        String type = configuration.getType();
        if (CanonicalStoryActionConfiguration.GIVE_HEALTH.equals(type)) {
            if (configuration.getNumber("amount") == 0) return true;
            player.setHealth(healthAfter(player.getHealth(), configuration.getNumber("amount"), player.getMaxHealth()));
            return true;
        }
        if (CanonicalStoryActionConfiguration.GIVE_BUFF.equals(type)) {
            if (configuration.getInteger("duration_delta") == 0 && configuration.getInteger("level_delta") == 0)
                return true;
            CanonicalBuffCatalog catalog = CanonicalBuffCatalog.get();
            Potion potion = configuration.isModBuff()
                ? catalog.modded(configuration.getText("mod_id"), configuration.getText("buff_name"))
                : catalog.vanilla(configuration.getText("buff"));
            PotionEffect previous = player.getActivePotionEffect(potion);
            int[] next = CanonicalBuffCatalog.delta(
                previous == null ? 0 : previous.getDuration(),
                previous == null ? 0 : previous.getAmplifier() + 1,
                configuration.getInteger("duration_delta"),
                configuration.getInteger("level_delta"));
            player.removePotionEffect(potion.id);
            if (next[0] > 0) player.addPotionEffect(new PotionEffect(potion.id, next[0], next[1] - 1));
            return true;
        }
        if (CanonicalStoryActionConfiguration.TELEPORT.equals(type)) {
            int dimension = configuration.getInteger("dimension_id");
            if (!DimensionManager.isDimensionRegistered(dimension)) return false;
            DimensionManager.initDimension(dimension);
            WorldServer target = DimensionManager.getWorld(dimension);
            if (target == null) return false;
            final double x = configuration.getNumber("x"), y = configuration.getNumber("y"),
                z = configuration.getNumber("z");
            final float yaw = player.rotationYaw, pitch = player.rotationPitch;
            if (player.ridingEntity != null) player.mountEntity(null);
            if (player.dimension != dimension) MinecraftServer.getServer()
                .getConfigurationManager()
                .transferPlayerToDimension(player, dimension, new Teleporter(target) {

                    @Override
                    public void placeInPortal(Entity entity, double ignoredX, double ignoredY, double ignoredZ,
                        float ignoredYaw) {
                        entity.setLocationAndAngles(x, y, z, yaw, pitch);
                    }
                });
            player.playerNetServerHandler.setPlayerLocation(x, y, z, yaw, pitch);
            player.fallDistance = 0;
            return true;
        }
        if (CanonicalStoryActionConfiguration.EXECUTE_COMMAND.equals(type)) {
            return MinecraftServer.getServer()
                .getCommandManager()
                .executeCommand(
                    commandContext(player),
                    configuration.getText("command")
                        .substring(1))
                > 0;
        }
        return false;
    }

    public static float healthAfter(float current, double delta, float maximum) {
        return (float) Math.max(0D, Math.min(maximum, current + delta));
    }

    /** Command-block-like permission 2, with this player's name, position and world. */
    public static ICommandSender commandContext(final EntityPlayerMP player) {
        return new ICommandSender() {

            public String getCommandSenderName() {
                return player.getCommandSenderName();
            }

            public IChatComponent func_145748_c_() {
                return player.func_145748_c_();
            }

            public void addChatMessage(IChatComponent message) {
                player.addChatMessage(message);
            }

            public boolean canCommandSenderUseCommand(int level, String command) {
                return level <= 2;
            }

            public ChunkCoordinates getPlayerCoordinates() {
                return player.getPlayerCoordinates();
            }

            public World getEntityWorld() {
                return player.worldObj;
            }
        };
    }
}
