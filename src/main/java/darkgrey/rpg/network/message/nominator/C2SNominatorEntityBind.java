package darkgrey.rpg.network.message.nominator;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorPermission;
import darkgrey.rpg.nominator.NominatorSavedData;
import darkgrey.rpg.nominator.NominatorService;
import io.netty.buffer.ByteBuf;

public final class C2SNominatorEntityBind implements IMessage {

    private int entityId;
    private UUID entityUuid;
    private String individualId;
    private List<String> groups;
    private String storyId;
    private long expectedRevision = -1L;
    private boolean transfer;
    private boolean typeScope;
    private String typeGroupId;
    private boolean addTypeGroup;

    public C2SNominatorEntityBind() {}

    public C2SNominatorEntityBind(int entityId, UUID entityUuid, String individualId, List<String> groups,
        String storyId) {
        this(entityId, entityUuid, individualId, groups, storyId, 0L, false);
    }

    public C2SNominatorEntityBind(int entityId, UUID entityUuid, String individualId, List<String> groups,
        String storyId, long expectedRevision, boolean transfer) {
        this.entityId = entityId;
        this.entityUuid = entityUuid;
        this.individualId = individualId;
        this.groups = groups == null ? new ArrayList<String>() : new ArrayList<String>(groups);
        this.storyId = storyId;
        this.expectedRevision = expectedRevision;
        this.transfer = transfer;
    }

    public C2SNominatorEntityBind(int entityId, UUID entityUuid, String individualId, List<String> groups,
        String storyId, long expectedRevision, boolean transfer, boolean typeScope, String typeGroupId,
        boolean addTypeGroup) {
        this(entityId, entityUuid, individualId, groups, storyId, expectedRevision, transfer);
        this.typeScope = typeScope;
        this.typeGroupId = typeGroupId;
        this.addTypeGroup = addTypeGroup;
    }

    public int getEntityId() {
        return entityId;
    }

    public UUID getEntityUuid() {
        return entityUuid;
    }

    public String getIndividualId() {
        return individualId;
    }

    public List<String> getGroups() {
        return new ArrayList<String>(groups);
    }

    public String getStoryId() {
        return storyId;
    }

    public long getExpectedRevision() {
        return expectedRevision;
    }

    public boolean isTransfer() {
        return transfer;
    }

    public boolean isTypeScope() {
        return typeScope;
    }

    public String getTypeGroupId() {
        return typeGroupId;
    }

    public boolean isAddTypeGroup() {
        return addTypeGroup;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        entityId = buffer.readInt();
        entityUuid = new UUID(buffer.readLong(), buffer.readLong());
        individualId = readString(buffer);
        storyId = readString(buffer);
        groups = new ArrayList<String>();
        int count = buffer.readByte() & 255;
        if (count > 32) throw new IllegalArgumentException("Too many groups.");
        for (int i = 0; i < count; i++) groups.add(readString(buffer));
        if (buffer.readableBytes() > 0) transfer = buffer.readBoolean();
        if (buffer.readableBytes() >= 8) expectedRevision = buffer.readLong();
        else if (buffer.readableBytes() > 0) throw new IllegalArgumentException("Invalid nominator revision.");
        if (buffer.isReadable()) {
            typeScope = buffer.readBoolean();
            if (typeScope) {
                typeGroupId = readString(buffer);
                if (!buffer.isReadable()) throw new IllegalArgumentException("Missing type group mode.");
                addTypeGroup = buffer.readBoolean();
            }
        }
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator bind data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeInt(entityId);
        buffer.writeLong(entityUuid.getMostSignificantBits());
        buffer.writeLong(entityUuid.getLeastSignificantBits());
        writeString(buffer, individualId);
        writeString(buffer, storyId);
        if (groups.size() > 32) throw new IllegalArgumentException("Too many groups.");
        buffer.writeByte(groups.size());
        for (String group : groups) writeString(buffer, group);
        buffer.writeBoolean(transfer);
        buffer.writeLong(expectedRevision);
        buffer.writeBoolean(typeScope);
        if (typeScope) {
            writeString(buffer, typeGroupId);
            buffer.writeBoolean(addTypeGroup);
        }
    }

    public static final class Handler implements IMessageHandler<C2SNominatorEntityBind, IMessage> {

        @Override
        public IMessage onMessage(final C2SNominatorEntityBind message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    Entity entity = player.worldObj.getEntityByID(message.entityId);
                    if (!NominatorPermission.canUse(player) || player.getHeldItem() == null
                        || player.getHeldItem()
                            .getItem() != ModItems.nominator
                        || entity == null
                        || !message.entityUuid.equals(entity.getUniqueID())
                        || entity.dimension != player.dimension
                        || player.getDistanceSqToEntity(entity) > 64.0D) return;
                    NominatorSavedData selections = NominatorSavedData.get();
                    if (message.expectedRevision < 0L || message.expectedRevision != selections.getRevision()) return;
                    String entityType = NominatorService.entityType(entity);
                    if (message.typeScope) {
                        if (!NominatorService.safeType(entityType)) return;
                        NominatorService.bindEntityTypeGroup(
                            true,
                            entityType,
                            message.typeGroupId,
                            message.addTypeGroup,
                            DarkGreyRpg.getProjectRepository()
                                .getSnapshot(),
                            selections);
                        return;
                    }
                    NominatorService.bindEntity(
                        true,
                        entity.getUniqueID(),
                        entityType,
                        entity.dimension,
                        message.individualId,
                        message.groups,
                        message.storyId,
                        message.transfer,
                        DarkGreyRpg.getProjectRepository()
                            .getSnapshot(),
                        NpcIdentitySavedData.get(),
                        selections);
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
