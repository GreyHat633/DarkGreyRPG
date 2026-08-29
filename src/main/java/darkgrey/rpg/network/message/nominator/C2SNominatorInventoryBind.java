package darkgrey.rpg.network.message.nominator;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorPermission;
import darkgrey.rpg.nominator.NominatorService;
import io.netty.buffer.ByteBuf;

public final class C2SNominatorInventoryBind implements IMessage {

    private String itemId;
    private String exactGroupId;
    private List<String> fuzzyGroups;
    private int selectedSlot;
    private long expectedRevision = -1L;

    public C2SNominatorInventoryBind() {}

    public C2SNominatorInventoryBind(int selectedSlot, String itemId, String exactGroupId, List<String> fuzzyGroups) {
        this(selectedSlot, itemId, exactGroupId, fuzzyGroups, -1L);
    }

    public C2SNominatorInventoryBind(int selectedSlot, String itemId, String exactGroupId, List<String> fuzzyGroups,
        long expectedRevision) {
        this.selectedSlot = selectedSlot;
        this.itemId = itemId;
        this.exactGroupId = exactGroupId;
        this.fuzzyGroups = fuzzyGroups == null ? new ArrayList<String>() : new ArrayList<String>(fuzzyGroups);
        this.expectedRevision = expectedRevision;
    }

    public C2SNominatorInventoryBind(String itemId, String exactGroupId, List<String> fuzzyGroups) {
        this(0, itemId, exactGroupId, fuzzyGroups);
    }

    public int getSelectedSlot() {
        return selectedSlot;
    }

    public String getItemId() {
        return itemId;
    }

    public String getExactGroupId() {
        return exactGroupId;
    }

    public List<String> getFuzzyGroups() {
        return new ArrayList<String>(fuzzyGroups);
    }

    public long getExpectedRevision() {
        return expectedRevision;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        selectedSlot = buffer.readByte();
        if (selectedSlot < 0 || selectedSlot > 35) throw new IllegalArgumentException("Invalid inventory slot.");
        itemId = readString(buffer);
        exactGroupId = readString(buffer);
        fuzzyGroups = new ArrayList<String>();
        int count = buffer.readByte() & 255;
        if (count > 32) throw new IllegalArgumentException("Too many groups.");
        for (int i = 0; i < count; i++) fuzzyGroups.add(readString(buffer));
        if (buffer.readableBytes() >= 8) expectedRevision = buffer.readLong();
        else if (buffer.readableBytes() > 0) throw new IllegalArgumentException("Invalid nominator revision.");
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator inventory bind data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (selectedSlot < 0 || selectedSlot > 35) throw new IllegalArgumentException("Invalid inventory slot.");
        buffer.writeByte(selectedSlot);
        writeString(buffer, itemId);
        writeString(buffer, exactGroupId);
        if (fuzzyGroups.size() > 32) throw new IllegalArgumentException("Too many groups.");
        buffer.writeByte(fuzzyGroups.size());
        for (String group : fuzzyGroups) writeString(buffer, group);
        buffer.writeLong(expectedRevision);
    }

    public static final class Handler implements IMessageHandler<C2SNominatorInventoryBind, IMessage> {

        @Override
        public IMessage onMessage(final C2SNominatorInventoryBind message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    if (!NominatorPermission.canUse(player) || message.selectedSlot < 0
                        || message.selectedSlot >= player.inventory.mainInventory.length
                        || player.getHeldItem() == null
                        || player.getHeldItem()
                            .getItem() != ModItems.nominator)
                        return;
                    darkgrey.rpg.item.identity.ItemIdentitySavedData saved = ItemIdentitySavedData.get();
                    if (message.expectedRevision < 0L || message.expectedRevision != saved.getRevision()) return;
                    ItemStack selected = player.inventory.mainInventory[message.selectedSlot];
                    if (selected == null || selected.getItem() == ModItems.nominator) return;
                    NominatorService.bindInventory(
                        true,
                        selected,
                        message.itemId,
                        message.exactGroupId,
                        message.fuzzyGroups,
                        DarkGreyRpg.getProjectRepository()
                            .getSnapshot(),
                        saved);
                }
            });
            return null;
        }
    }

    private static String readString(ByteBuf b) {
        int size = b.readShort();
        if (size < 0 || size > 256) throw new IllegalArgumentException("Invalid nominator text.");
        byte[] bytes = new byte[size];
        b.readBytes(bytes);
        return new String(bytes, java.nio.charset.StandardCharsets.UTF_8);
    }

    private static void writeString(ByteBuf b, String value) {
        byte[] bytes = (value == null ? "" : value).getBytes(java.nio.charset.StandardCharsets.UTF_8);
        if (bytes.length > 256) throw new IllegalArgumentException("Nominator text is too long.");
        b.writeShort(bytes.length);
        b.writeBytes(bytes);
    }
}
