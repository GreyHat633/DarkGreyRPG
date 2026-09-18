package darkgrey.rpg.gramophone;

import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.tileentity.TileEntity;

public final class TileGramophone extends TileEntity {

    public String instance = UUID.randomUUID()
        .toString();
    public long revision;
    public int radius = 16;
    public boolean enabled = true;
    public boolean redstone;
    public String source = "";
    public boolean ready;
    public boolean restoring;
    public long retryRestore;
    public String recoveryError = "正在恢复设备配置…";

    @Override
    public boolean canUpdate() {
        return false;
    }

    @Override
    public void validate() {
        super.validate();
        GramophoneServer.add(this);
    }

    @Override
    public void invalidate() {
        GramophoneServer.remove(this);
        super.invalidate();
    }

    @Override
    public void onChunkUnload() {
        GramophoneServer.remove(this);
        super.onChunkUnload();
    }

    @Override
    public void writeToNBT(NBTTagCompound tag) {
        super.writeToNBT(tag);
        tag.setString("instance", instance);
        tag.setLong("revision", revision);
        tag.setInteger("radius", radius);
        tag.setBoolean("enabled", enabled);
        tag.setBoolean("redstone", redstone);
        tag.setString("source", source);
    }

    @Override
    public void readFromNBT(NBTTagCompound tag) {
        super.readFromNBT(tag);
        if (tag.hasKey("instance")) instance = tag.getString("instance");
        revision = Math.max(0, tag.getLong("revision"));
        radius = tag.hasKey("radius") ? tag.getInteger("radius") : 16;
        if (radius < 0 || radius > GramophoneServer.MAX_RADIUS) radius = 16;
        enabled = !tag.hasKey("enabled") || tag.getBoolean("enabled");
        redstone = tag.getBoolean("redstone");
        source = tag.getString("source");
    }

    public GramophonePacket snapshot(int operation) {
        GramophonePacket packet = new GramophonePacket();
        packet.operation = operation;
        packet.dimension = worldObj.provider.dimensionId;
        packet.x = xCoord;
        packet.y = yCoord;
        packet.z = zCoord;
        packet.instance = instance;
        packet.revision = revision;
        packet.radius = radius;
        packet.enabled = enabled;
        packet.redstone = redstone;
        packet.powered = !redstone
            || (worldObj.checkChunksExist(xCoord - 2, yCoord - 2, zCoord - 2, xCoord + 2, yCoord + 2, zCoord + 2)
                && worldObj.isBlockIndirectlyGettingPowered(xCoord, yCoord, zCoord));
        packet.source = source;
        if (!ready) packet.status = recoveryError;
        return packet;
    }
}
