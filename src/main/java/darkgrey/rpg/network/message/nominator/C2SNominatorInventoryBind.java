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
import darkgrey.rpg.nominator.NominatorResult;
import darkgrey.rpg.nominator.NominatorService;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;
import darkgrey.rpg.runtime.ChatMessages;
import io.netty.buffer.ByteBuf;

public final class C2SNominatorInventoryBind implements IMessage {

    private String itemId;
    private String exactGroupId;
    private List<String> fuzzyGroups;
    private int selectedSlot;
    private long expectedRevision = -1L;
    private String packageId;
    private long expectedCatalogRevision = -1L;

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

    public C2SNominatorInventoryBind(String packageId, String itemId, String exactGroupId, long expectedRevision,
        long expectedCatalogRevision) {
        this(-1, itemId, exactGroupId, java.util.Collections.<String>emptyList(), expectedRevision);
        this.packageId = packageId;
        this.expectedCatalogRevision = expectedCatalogRevision;
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

    public String getPackageId() {
        return packageId;
    }

    public long getExpectedCatalogRevision() {
        return expectedCatalogRevision;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        selectedSlot = buffer.readByte();
        if (selectedSlot < -1 || selectedSlot > 35) throw new IllegalArgumentException("Invalid inventory slot.");
        itemId = readString(buffer);
        exactGroupId = readString(buffer);
        fuzzyGroups = new ArrayList<String>();
        int count = buffer.readByte() & 255;
        if (count > 32) throw new IllegalArgumentException("Too many groups.");
        for (int i = 0; i < count; i++) fuzzyGroups.add(readString(buffer));
        if (buffer.readableBytes() >= 8) expectedRevision = buffer.readLong();
        else if (buffer.readableBytes() > 0) throw new IllegalArgumentException("Invalid nominator revision.");
        if (buffer.isReadable()) {
            packageId = readString(buffer);
            if (buffer.readableBytes() < 8) throw new IllegalArgumentException("Missing nominator catalog revision.");
            expectedCatalogRevision = buffer.readLong();
        }
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator inventory bind data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (selectedSlot < -1 || selectedSlot > 35) throw new IllegalArgumentException("Invalid inventory slot.");
        buffer.writeByte(selectedSlot);
        writeString(buffer, itemId);
        writeString(buffer, exactGroupId);
        if (fuzzyGroups.size() > 32) throw new IllegalArgumentException("Too many groups.");
        buffer.writeByte(fuzzyGroups.size());
        for (String group : fuzzyGroups) writeString(buffer, group);
        buffer.writeLong(expectedRevision);
        writeString(buffer, packageId);
        buffer.writeLong(expectedCatalogRevision);
    }

    public static final class Handler implements IMessageHandler<C2SNominatorInventoryBind, IMessage> {

        @Override
        public IMessage onMessage(final C2SNominatorInventoryBind message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    if (!(player.openContainer instanceof ContainerNominatorInventory)) return;
                    try {
                        if (!NominatorPermission.canUse(player) || !player.inventory.hasItem(ModItems.nominator)) {
                            ChatMessages.error(player, "没有使用指名器的权限或指名器已不在背包中。");
                            return;
                        }
                        if (message.selectedSlot != -1 || message.fuzzyGroups == null
                            || !message.fuzzyGroups.isEmpty()) {
                            ChatMessages.error(player, "物品指名请求不是当前目标槽格式。");
                            return;
                        }
                        ItemIdentitySavedData saved = ItemIdentitySavedData.get();
                        if (message.expectedRevision < 0L || message.expectedRevision != saved.getRevision()) {
                            ChatMessages.error(player, "物品指名数据已更新，请重新打开指名器后再试。");
                            return;
                        }
                        darkgrey.rpg.project.ProjectRepository repository = DarkGreyRpg.getProjectRepository();
                        if (message.expectedCatalogRevision < 0L
                            || message.expectedCatalogRevision != repository.getSnapshotRevision()) {
                            ChatMessages.error(player, "故事包目录已更新，请重新打开指名器后再试。");
                            return;
                        }
                        boolean itemSelected = !blank(message.itemId);
                        boolean groupSelected = !blank(message.exactGroupId);
                        if (itemSelected == groupSelected) {
                            ChatMessages.error(player, "必须从当前故事包中选择一个物品或物品组。");
                            return;
                        }
                        darkgrey.rpg.nominator.NominatorCatalog.PackageChoice choice = darkgrey.rpg.nominator.NominatorCatalog
                            .from(
                                repository.getSnapshot(),
                                DarkGreyRpg.getStoryPackageLoader()
                                    .getPackages())
                            .getPackageChoice(message.packageId);
                        if (choice == null || itemSelected && !choice.containsItem(message.itemId)
                            || groupSelected && !choice.containsItemGroup(message.exactGroupId)) {
                            ChatMessages.error(player, "所选物品资源不属于当前故事包，请重新选择。");
                            return;
                        }
                        ItemStack selected = ((ContainerNominatorInventory) player.openContainer).getTargetInventory()
                            .getStackInSlot(0);
                        if (selected == null || selected.getItem() == ModItems.nominator) {
                            ChatMessages.error(player, "请先把一个物品放入目标槽。");
                            return;
                        }
                        NominatorResult result = NominatorService.bindInventory(
                            true,
                            selected,
                            message.itemId,
                            message.exactGroupId,
                            java.util.Collections.<String>emptyList(),
                            repository.getSnapshot(),
                            saved);
                        if (result == null || !result.isAccepted()) ChatMessages
                            .error(player, "物品指名失败：" + (result == null ? "服务器未返回结果。" : result.getExplanation()));
                        else ChatMessages.success(player, result.getExplanation());
                    } catch (RuntimeException exception) {
                        DarkGreyRpg.LOG.error("Item Nominator request failed.", exception);
                        ChatMessages.error(player, "物品指名失败：服务器拒绝了该请求。");
                    } finally {
                        // Server closes only after capture/bind; the Container then returns the target stack.
                        player.closeScreen();
                    }
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

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
