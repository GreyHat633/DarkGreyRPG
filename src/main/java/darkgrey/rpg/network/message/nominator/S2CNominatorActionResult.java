package darkgrey.rpg.network.message.nominator;

import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;

public final class S2CNominatorActionResult implements IMessage {

    public NBTTagCompound data;
    public NominatorCatalog catalog;

    public S2CNominatorActionResult() {}

    public S2CNominatorActionResult(NBTTagCompound data, NominatorCatalog catalog) {
        this.data = data;
        this.catalog = catalog;
    }

    public void fromBytes(ByteBuf b) {
        data = ByteBufUtils.readTag(b);
        catalog = NominatorCatalogCodec.read(b);
        if (data == null || b.isReadable()) throw new IllegalArgumentException("Invalid result");
    }

    public void toBytes(ByteBuf b) {
        ByteBufUtils.writeTag(b, data);
        NominatorCatalogCodec.write(b, catalog);
    }

    public static final class Handler implements IMessageHandler<S2CNominatorActionResult, IMessage> {

        public IMessage onMessage(final S2CNominatorActionResult m, MessageContext ctx) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                public void run() {
                    DarkGreyRpg.proxy.acceptNominatorResult(m.data, m.catalog);
                }
            });
            return null;
        }
    }
}
