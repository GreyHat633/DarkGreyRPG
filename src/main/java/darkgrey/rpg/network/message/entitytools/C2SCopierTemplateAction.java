package darkgrey.rpg.network.message.entitytools;

import java.nio.charset.StandardCharsets;
import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.util.ChatComponentText;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.entitytools.CopierState;
import darkgrey.rpg.entitytools.EntityTemplate;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Selects or deletes one copier template; the held server ItemStack remains authoritative. */
public final class C2SCopierTemplateAction implements IMessage {

    public enum Operation {
        SELECT,
        DELETE
    }

    private int hotbarSlot;
    private Operation operation;
    private int templateIndex;
    private int expectedCount;
    private String expectedEntityType;

    public C2SCopierTemplateAction() {}

    public C2SCopierTemplateAction(int hotbarSlot, Operation operation, int templateIndex, int expectedCount,
        String expectedEntityType) {
        validate(hotbarSlot, operation, templateIndex, expectedCount, expectedEntityType);
        this.hotbarSlot = hotbarSlot;
        this.operation = operation;
        this.templateIndex = templateIndex;
        this.expectedCount = expectedCount;
        this.expectedEntityType = expectedEntityType;
    }

    public int getHotbarSlot() {
        return hotbarSlot;
    }

    public Operation getOperation() {
        return operation;
    }

    public int getTemplateIndex() {
        return templateIndex;
    }

    public int getExpectedCount() {
        return expectedCount;
    }

    public String getExpectedEntityType() {
        return expectedEntityType;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        int slot = buffer.readByte();
        int operationId = buffer.readByte();
        if (operationId < 0 || operationId >= Operation.values().length)
            throw new IllegalArgumentException("Invalid copier operation.");
        int index = buffer.readInt();
        int count = buffer.readInt();
        String type = readString(buffer);
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing copier packet bytes.");
        Operation decoded = Operation.values()[operationId];
        validate(slot, decoded, index, count, type);
        hotbarSlot = slot;
        operation = decoded;
        templateIndex = index;
        expectedCount = count;
        expectedEntityType = type;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(hotbarSlot, operation, templateIndex, expectedCount, expectedEntityType);
        buffer.writeByte(hotbarSlot);
        buffer.writeByte(operation.ordinal());
        buffer.writeInt(templateIndex);
        buffer.writeInt(expectedCount);
        writeString(buffer, expectedEntityType);
    }

    public static final class Handler implements IMessageHandler<C2SCopierTemplateAction, IMessage> {

        @Override
        public IMessage onMessage(final C2SCopierTemplateAction message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    if (player.inventory.currentItem != message.hotbarSlot) return;
                    ItemStack held = player.getHeldItem();
                    if (held == null || held.getItem() != ModItems.copier) return;
                    try {
                        CopierState state = ItemCopier.loadState(held);
                        List<EntityTemplate> templates = state.getTemplates();
                        if (templates.size() != message.expectedCount || message.templateIndex < 0
                            || message.templateIndex >= templates.size()
                            || !message.expectedEntityType.equals(
                                templates.get(message.templateIndex)
                                    .getEntityType()))
                            throw new IllegalStateException("复制器模板列表已变化，请重新打开管理界面。");
                        if (message.operation == Operation.SELECT) {
                            state.select(message.templateIndex);
                            player.addChatMessage(
                                new ChatComponentText(
                                    "已选择模板 " + (message.templateIndex + 1) + ": " + message.expectedEntityType));
                        } else {
                            state.remove(message.templateIndex);
                            player.addChatMessage(new ChatComponentText("已删除模板: " + message.expectedEntityType));
                        }
                        ItemCopier.saveState(held, state);
                        player.inventory.markDirty();
                    } catch (RuntimeException exception) {
                        String detail = exception.getMessage();
                        player.addChatMessage(
                            new ChatComponentText(
                                "§c复制器模板操作失败: " + (detail == null || detail.trim()
                                    .isEmpty() ? "请求无效" : detail)));
                    }
                }
            });
            return null;
        }
    }

    private static void validate(int slot, Operation operation, int index, int count, String type) {
        if (slot < 0 || slot > 8) throw new IllegalArgumentException("Invalid copier hotbar slot.");
        if (operation == null) throw new IllegalArgumentException("Copier operation is required.");
        if (count <= 0 || count > 1024 || index < 0 || index >= count)
            throw new IllegalArgumentException("Invalid copier template index.");
        if (type == null || type.trim()
            .isEmpty()) throw new IllegalArgumentException("Copier entity type is required.");
    }

    private static String readString(ByteBuf buffer) {
        int length = buffer.readUnsignedShort();
        if (length == 0 || length > 256 || buffer.readableBytes() < length)
            throw new IllegalArgumentException("Invalid copier entity type.");
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }

    private static void writeString(ByteBuf buffer, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        if (bytes.length == 0 || bytes.length > 256) throw new IllegalArgumentException("Invalid copier entity type.");
        buffer.writeShort(bytes.length);
        buffer.writeBytes(bytes);
    }
}
