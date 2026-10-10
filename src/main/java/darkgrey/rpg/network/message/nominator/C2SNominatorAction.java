package darkgrey.rpg.network.message.nominator;

import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.ResourceAddressNbt;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorActions;
import io.netty.buffer.ByteBuf;

/** Versioned, correlated 0.3.2.3 operation; server validates every mutation. */
public final class C2SNominatorAction implements IMessage {

    public NBTTagCompound data;

    public C2SNominatorAction() {}

    public C2SNominatorAction(NBTTagCompound data) {
        this.data = (NBTTagCompound) data.copy();
    }

    public void fromBytes(ByteBuf b) {
        data = ByteBufUtils.readTag(b);
        if (data == null || b.isReadable()) throw new IllegalArgumentException("Invalid action packet");
        ResourceAddressNbt.requireFormat(data);
        if (data.hasKey("resource")) {
            String resource = ResourceAddressNbt.read(data, "resource", kind(data));
            data.setString("resource", resource);
        }
        UUID.fromString(data.getString("token"));
        if (data.getString("resource")
            .length() > 256
            || data.getString("package")
                .length() > 256)
            throw new IllegalArgumentException("Invalid resource");
    }

    public void toBytes(ByteBuf b) {
        NBTTagCompound wire = (NBTTagCompound) data.copy();
        wire.setString("identity_format", ResourceAddressNbt.IDENTITY_FORMAT);
        if (data.hasKey("resource")) {
            wire.setTag("resource", ResourceAddressNbt.write(data.getString("resource"), kind(data)));
        }
        ByteBufUtils.writeTag(b, wire);
    }

    private static ResourceAddress.Kind kind(NBTTagCompound value) {
        String type = value.getString("type");
        if ("NPC".equals(type) || "Group".equals(type)) return ResourceAddress.Kind.ACTOR;
        if ("Item".equals(type)) return ResourceAddress.Kind.ITEM;
        if ("Item Group".equals(type)) return ResourceAddress.Kind.ITEM_GROUP;
        throw new IllegalArgumentException("Unknown Nominator resource type");
    }

    public static final class Handler implements IMessageHandler<C2SNominatorAction, IMessage> {

        public IMessage onMessage(final C2SNominatorAction m, MessageContext ctx) {
            final EntityPlayerMP player = ctx.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(player, new Runnable() {

                public void run() {
                    NominatorActions.handle(player, m.data);
                }
            }, () -> darkgrey.rpg.nominator.NominatorActions.queueRejected(player, m.data));
            return null;
        }
    }
}
