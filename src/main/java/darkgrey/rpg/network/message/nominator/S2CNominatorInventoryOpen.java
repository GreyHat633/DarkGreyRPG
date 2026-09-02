package darkgrey.rpg.network.message.nominator;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;

/** Server-authoritative inventory catalog snapshot. */
public final class S2CNominatorInventoryOpen implements IMessage {

    private long revision;
    private long catalogRevision = -1L;
    private int selectedSlot;
    private NominatorCatalog catalog;

    public S2CNominatorInventoryOpen() {}

    private S2CNominatorInventoryOpen(long revision, long catalogRevision, int selectedSlot, NominatorCatalog catalog) {
        this.revision = revision;
        this.catalogRevision = catalogRevision;
        this.selectedSlot = selectedSlot;
        this.catalog = catalog;
    }

    static S2CNominatorInventoryOpen from(EntityPlayerMP player) {
        return new S2CNominatorInventoryOpen(
            ItemIdentitySavedData.get()
                .getRevision(),
            DarkGreyRpg.getProjectRepository()
                .getSnapshotRevision(),
            -1,
            NominatorCatalog.from(
                DarkGreyRpg.getProjectRepository()
                    .getSnapshot(),
                DarkGreyRpg.getStoryPackageLoader()
                    .getPackages()));
    }

    public long getRevision() {
        return revision;
    }

    public int getSelectedSlot() {
        return selectedSlot;
    }

    public long getCatalogRevision() {
        return catalogRevision;
    }

    public NominatorCatalog getCatalog() {
        return catalog;
    }

    @Override
    public void fromBytes(ByteBuf b) {
        revision = b.readLong();
        catalogRevision = b.readLong();
        selectedSlot = b.readByte();
        if (selectedSlot < -1 || selectedSlot > 35) throw new IllegalArgumentException("Invalid inventory slot.");
        catalog = NominatorCatalogCodec.read(b);
        if (b.isReadable()) throw new IllegalArgumentException("Trailing nominator catalog data.");
    }

    @Override
    public void toBytes(ByteBuf b) {
        b.writeLong(revision);
        b.writeLong(catalogRevision);
        b.writeByte(selectedSlot);
        NominatorCatalogCodec.write(b, catalog);
    }

    public static final class Handler implements IMessageHandler<S2CNominatorInventoryOpen, IMessage> {

        @Override
        public IMessage onMessage(final S2CNominatorInventoryOpen message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    DarkGreyRpg.proxy.openNominatorInventoryGui(
                        message.catalog,
                        message.revision,
                        message.catalogRevision,
                        message.selectedSlot);
                }
            });
            return null;
        }
    }
}
