package darkgrey.rpg.gramophone;

import java.util.Collections;
import java.util.IdentityHashMap;
import java.util.Set;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.tileentity.TileEntity;
import net.minecraft.world.World;
import net.minecraftforge.event.world.BlockEvent;
import net.minecraftforge.event.world.WorldEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

/** Only loaded devices are indexed. Reading/synchronizing never loads surrounding chunks. */
public final class GramophoneServer {

    public static final int MAX_RADIUS = 128;
    private static final Set<TileGramophone> LOADED = Collections
        .newSetFromMap(new IdentityHashMap<TileGramophone, Boolean>());

    static boolean loaded(TileGramophone tile) {
        return LOADED.contains(tile);
    }

    public static void add(TileGramophone tile) {
        if (tile.getWorldObj() != null && !tile.getWorldObj().isRemote) LOADED.add(tile);
    }

    public static void remove(TileGramophone tile) {
        if (tile.getWorldObj() != null && !tile.getWorldObj().isRemote) LOADED.remove(tile);
    }

    public static boolean allowed(EntityPlayer player) {
        return player != null && (player.capabilities.isCreativeMode || player.canCommandSenderUseCommand(2, "dgrpg"));
    }

    static void changed(TileGramophone tile) {
        World world = tile.getWorldObj();
        if (world == null || world.isRemote || !tile.ready || tile.isInvalid() || !loaded(tile)) return;
        GramophonePacket state = tile.snapshot(GramophonePacket.STATE);
        for (Object object : world.playerEntities) {
            if (!(object instanceof EntityPlayerMP)) continue;
            EntityPlayerMP player = (EntityPlayerMP) object;
            if (GramophonePlayback.contains(
                player.dimension,
                world.provider.dimensionId,
                tile.xCoord,
                tile.yCoord,
                tile.zCoord,
                tile.radius + 8,
                player.posX,
                player.posY,
                player.posZ)) GramophoneNetwork.CHANNEL.sendTo(state, player);
        }
    }

    public static void save(EntityPlayerMP player, GramophonePacket request) {
        if (request.operation != GramophonePacket.SAVE && request.operation != GramophonePacket.DELETE) return;
        String failure = null;
        TileGramophone tile = null;
        World world = player.worldObj;
        if (!allowed(player)) failure = "需要 OP 或创造模式权限";
        else if (world.provider.dimensionId != request.dimension
            || player.getDistanceSq(request.x + 0.5, request.y + 0.5, request.z + 0.5) > 64
            || !world.blockExists(request.x, request.y, request.z)) failure = "留声机不在可编辑距离内";
        else {
            TileEntity found = world.getTileEntity(request.x, request.y, request.z);
            if (!(found instanceof TileGramophone)) failure = "留声机已被移除";
            else {
                tile = (TileGramophone) found;
                if (!tile.ready || GramophoneLocalServer.committing(request)
                    || !tile.instance.equals(request.instance)
                    || tile.revision != request.revision) failure = "配置已变化或正在保存，请重新打开留声机";
                else if (request.radius < 0 || request.radius > MAX_RADIUS) failure = "范围必须为 0–128 格";
            }
        }
        String source = "";
        if (failure == null && request.operation == GramophonePacket.SAVE
            && !request.source.trim()
                .isEmpty()) {
            try {
                if (request.source.startsWith("local:") && tile != null && request.source.equals(tile.source))
                    source = tile.source;
                else source = OnlineMusicSource.parse(request.source)
                    .canonical();
            } catch (IllegalArgumentException exception) {
                failure = exception.getMessage();
            }
        }
        if (failure == null && tile != null) {
            request.source = source;
            GramophoneLocalServer.commit(player, tile, request, null);
        } else {
            request.operation = GramophonePacket.RESULT;
            request.status = failure == null ? "无法保存" : failure;
            GramophoneNetwork.CHANNEL.sendTo(request, player);
        }
    }

    @SubscribeEvent
    public void breakBlock(BlockEvent.BreakEvent event) {
        if (event.block instanceof BlockGramophone && !allowed(event.getPlayer())) event.setCanceled(true);
    }

    @SubscribeEvent
    public void placeBlock(BlockEvent.PlaceEvent event) {
        if (event.placedBlock instanceof BlockGramophone && !allowed(event.player)) event.setCanceled(true);
    }

    @SubscribeEvent
    public void unload(WorldEvent.Unload event) {
        GramophoneLocalServer.unload(event.world);
        if (!event.world.isRemote) LOADED.removeIf(tile -> tile.getWorldObj() == event.world);
    }

    @SubscribeEvent
    public void tick(TickEvent.WorldTickEvent event) {
        if (event.phase != TickEvent.Phase.END || event.world.isRemote || event.world.getTotalWorldTime() % 10 != 0)
            return;
        GramophoneLocalServer.tick();
        for (TileGramophone tile : LOADED) if (tile.getWorldObj() == event.world && !tile.isInvalid()
            && !tile.ready
            && !tile.restoring
            && System.nanoTime() >= tile.retryRestore) GramophoneLocalServer.restore(tile);
        for (Object object : event.world.playerEntities) {
            if (!(object instanceof EntityPlayerMP)) continue;
            EntityPlayerMP player = (EntityPlayerMP) object;
            GramophonePacket begin = new GramophonePacket();
            begin.operation = GramophonePacket.BEGIN;
            begin.dimension = event.world.provider.dimensionId;
            GramophoneNetwork.CHANNEL.sendTo(begin, player);
            for (TileGramophone tile : LOADED) {
                if (tile.getWorldObj() != event.world || tile.isInvalid() || !tile.ready) continue;
                // Include a fade margin; players can hear an indexed device outside their chunk watch radius.
                if (GramophonePlayback.contains(
                    player.dimension,
                    event.world.provider.dimensionId,
                    tile.xCoord,
                    tile.yCoord,
                    tile.zCoord,
                    tile.radius + 8,
                    player.posX,
                    player.posY,
                    player.posZ)) GramophoneNetwork.CHANNEL.sendTo(tile.snapshot(GramophonePacket.STATE), player);
            }
            GramophonePacket end = new GramophonePacket();
            end.operation = GramophonePacket.END;
            end.dimension = event.world.provider.dimensionId;
            GramophoneNetwork.CHANNEL.sendTo(end, player);
        }
    }
}
