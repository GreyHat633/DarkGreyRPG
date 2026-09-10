package darkgrey.rpg.network.message.nominator;

import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
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
        UUID.fromString(data.getString("token"));
        if (data.getString("resource")
            .length() > 256
            || data.getString("package")
                .length() > 256)
            throw new IllegalArgumentException("Invalid resource");
    }

    public void toBytes(ByteBuf b) {
        ByteBufUtils.writeTag(b, data);
    }

    public static final class Handler implements IMessageHandler<C2SNominatorAction, IMessage> {

        public IMessage onMessage(final C2SNominatorAction m, MessageContext ctx) {
            final EntityPlayerMP player = ctx.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                public void run() {
                    NominatorActions.handle(player, m.data);
                }
            });
            return null;
        }
    }
}
