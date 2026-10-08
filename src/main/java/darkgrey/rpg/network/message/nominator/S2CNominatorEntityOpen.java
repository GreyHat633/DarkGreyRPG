package darkgrey.rpg.network.message.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;
import io.netty.buffer.ByteBuf;

/** Server snapshot used to initialize the entity nominator GUI. */
public final class S2CNominatorEntityOpen implements IMessage {

    public static final int PROTOCOL_MARKER = 0x44475240;

    private int entityId;
    private UUID entityUuid;
    private long revision;
    private long catalogRevision = -1L;
    private String individual;
    private List<String> groups = Collections.emptyList();
    private String story;
    private String displayName;
    private String entityType;
    private NominatorCatalog catalog;

    public S2CNominatorEntityOpen() {}

    public S2CNominatorEntityOpen(int entityId, UUID uuid, long revision, String individual, List<String> groups,
        String story) {
        if (uuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        this.entityId = entityId;
        this.entityUuid = uuid;
        this.revision = revision;
        this.individual = individual;
        this.groups = groups == null ? Collections.<String>emptyList()
            : Collections.unmodifiableList(new ArrayList<String>(groups));
        this.story = story;
        this.catalog = new NominatorCatalog(
            Collections.<NominatorCatalog.Story>emptyList(),
            Collections.<NominatorCatalog.Actor>emptyList(),
            Collections.<NominatorCatalog.Item>emptyList(),
            Collections.<NominatorCatalog.Item>emptyList());
    }

    public S2CNominatorEntityOpen(int entityId, UUID uuid, long revision, String displayName, String entityType,
        String individual, List<String> groups, String story, NominatorCatalog catalog) {
        this(entityId, uuid, revision, individual, groups, story);
        this.displayName = displayName;
        this.entityType = entityType;
        this.catalog = catalog;
    }

    public S2CNominatorEntityOpen(int entityId, UUID uuid, long revision, long catalogRevision, String displayName,
        String entityType, String individual, List<String> groups, String story, NominatorCatalog catalog) {
        this(entityId, uuid, revision, displayName, entityType, individual, groups, story, catalog);
        this.catalogRevision = catalogRevision;
    }

    static S2CNominatorEntityOpen from(int entityId, UUID uuid, NominatorSavedData selections) {
        S2CNominatorEntityOpen packet = new S2CNominatorEntityOpen();
        packet.entityId = entityId;
        packet.entityUuid = uuid;
        packet.revision = selections.getRevision();
        NominatorEntityBinding binding = selections.get(uuid);
        if (binding != null) {
            packet.individual = binding.getIndividualId();
            packet.groups = new ArrayList<String>(binding.getGroupIds());
            packet.story = binding.getStoryId();
        }
        return packet;
    }

    static S2CNominatorEntityOpen from(net.minecraft.entity.Entity entity, NominatorSavedData selections,
        NominatorCatalog catalog) {
        S2CNominatorEntityOpen packet = from(entity.getEntityId(), entity.getUniqueID(), selections);
        packet.displayName = entity.getCommandSenderName();
        packet.entityType = darkgrey.rpg.nominator.NominatorService.entityType(entity);
        packet.catalog = catalog;
        packet.catalogRevision = DarkGreyRpg.getProjectRepository()
            .getSnapshotRevision();
        return packet;
    }

    public int getEntityId() {
        return entityId;
    }

    public UUID getEntityUuid() {
        return entityUuid;
    }

    public long getRevision() {
        return revision;
    }

    public long getCatalogRevision() {
        return catalogRevision;
    }

    public String getIndividualId() {
        return individual;
    }

    public List<String> getGroups() {
        return new ArrayList<String>(groups);
    }

    public String getStoryId() {
        return story;
    }

    public String getDisplayName() {
        return displayName;
    }

    public String getEntityType() {
        return entityType;
    }

    public NominatorCatalog getCatalog() {
        return catalog;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readInt() != PROTOCOL_MARKER)
            throw new IllegalArgumentException("Unsupported Nominator identity protocol");
        entityId = buffer.readInt();
        entityUuid = new UUID(buffer.readLong(), buffer.readLong());
        revision = buffer.readLong();
        individual = NominatorIdentityCodec.read(buffer, ResourceAddress.Kind.ACTOR);
        story = NominatorIdentityCodec.story(buffer);
        int count = buffer.readUnsignedByte();
        if (count > 32) throw new IllegalArgumentException("Too many groups.");
        List<String> decoded = new ArrayList<String>();
        for (int i = 0; i < count; i++) decoded.add(NominatorIdentityCodec.read(buffer, ResourceAddress.Kind.ACTOR));
        groups = Collections.unmodifiableList(decoded);
        displayName = readString(buffer);
        entityType = readString(buffer);
        catalogRevision = buffer.readLong();
        catalog = NominatorCatalogCodec.read(buffer);
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator catalog data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeInt(PROTOCOL_MARKER);
        buffer.writeInt(entityId);
        buffer.writeLong(entityUuid.getMostSignificantBits());
        buffer.writeLong(entityUuid.getLeastSignificantBits());
        buffer.writeLong(revision);
        NominatorIdentityCodec.write(buffer, individual, ResourceAddress.Kind.ACTOR);
        NominatorIdentityCodec.story(buffer, story);
        if (groups.size() > 32) throw new IllegalArgumentException("Too many groups.");
        buffer.writeByte(groups.size());
        for (String group : groups) NominatorIdentityCodec.write(buffer, group, ResourceAddress.Kind.ACTOR);
        writeString(buffer, displayName);
        writeString(buffer, entityType);
        buffer.writeLong(catalogRevision);
        NominatorCatalogCodec.write(
            buffer,
            catalog == null
                ? new NominatorCatalog(
                    Collections.<NominatorCatalog.Story>emptyList(),
                    Collections.<NominatorCatalog.Actor>emptyList(),
                    Collections.<NominatorCatalog.Item>emptyList(),
                    Collections.<NominatorCatalog.Item>emptyList())
                : catalog);
    }

    private static String readString(ByteBuf buffer) {
        int size = buffer.readUnsignedShort();
        if (size > 256) throw new IllegalArgumentException("Nominator text is too long.");
        byte[] bytes = new byte[size];
        buffer.readBytes(bytes);
        return size == 0 ? null : new String(bytes, java.nio.charset.StandardCharsets.UTF_8);
    }

    private static void writeString(ByteBuf buffer, String value) {
        byte[] bytes = (value == null ? "" : value).getBytes(java.nio.charset.StandardCharsets.UTF_8);
        if (bytes.length > 256) throw new IllegalArgumentException("Nominator text is too long.");
        buffer.writeShort(bytes.length);
        buffer.writeBytes(bytes);
    }

    public static final class Handler implements IMessageHandler<S2CNominatorEntityOpen, IMessage> {

        @Override
        public IMessage onMessage(final S2CNominatorEntityOpen message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    DarkGreyRpg.proxy.openNominatorEntityGui(
                        message.entityId,
                        message.entityUuid,
                        message.displayName,
                        message.entityType,
                        message.individual,
                        message.groups,
                        message.story,
                        message.revision,
                        message.catalogRevision,
                        message.catalog);
                }
            });
            return null;
        }
    }
}
