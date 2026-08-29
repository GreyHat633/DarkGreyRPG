package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import io.netty.buffer.ByteBuf;

/** Server-authoritative action: transport identity, cursor, explicit action kind, and stable option ID. */
public final class CanonicalSessionAction implements IMessage {

    public enum Kind {
        CONTINUE,
        CHOICE
    }

    private long transportId;
    private String storyId;
    private String currentNodeId;
    private Kind kind;
    private String optionId;

    public CanonicalSessionAction() {}

    public CanonicalSessionAction(long transportId, String storyId, String currentNodeId, Kind kind, String optionId) {
        validate(transportId, storyId, currentNodeId, kind, optionId);
        this.transportId = transportId;
        this.storyId = storyId;
        this.currentNodeId = currentNodeId;
        this.kind = kind;
        this.optionId = optionId;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 8) throw CanonicalSessionNetworkCodec.invalid("truncated transport_id");
        long decodedTransportId = buffer.readLong();
        String decodedStoryId = CanonicalSessionNetworkCodec
            .readField(buffer, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        String decodedNodeId = CanonicalSessionNetworkCodec
            .readField(buffer, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        Kind decodedKind = Kind.values()[CanonicalSessionNetworkCodec
            .readEnum(buffer, Kind.values().length, "action kind")];
        String decodedOptionId = null;
        if (decodedKind == Kind.CHOICE) decodedOptionId = CanonicalSessionNetworkCodec
            .readField(buffer, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        validate(decodedTransportId, decodedStoryId, decodedNodeId, decodedKind, decodedOptionId);
        transportId = decodedTransportId;
        storyId = decodedStoryId;
        currentNodeId = decodedNodeId;
        kind = decodedKind;
        optionId = decodedOptionId;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(transportId, storyId, currentNodeId, kind, optionId);
        buffer.writeLong(transportId);
        CanonicalSessionNetworkCodec.writeField(buffer, storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec
            .writeField(buffer, currentNodeId, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        buffer.writeByte(kind.ordinal());
        if (kind == Kind.CHOICE) CanonicalSessionNetworkCodec
            .writeField(buffer, optionId, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
    }

    public long getTransportId() {
        return transportId;
    }

    public long getSessionId() {
        return transportId;
    }

    public String getStoryId() {
        return storyId;
    }

    public String getCurrentNodeId() {
        return currentNodeId;
    }

    public String getNodeId() {
        return currentNodeId;
    }

    public Kind getKind() {
        return kind;
    }

    public String getOptionId() {
        return optionId;
    }

    private static void validate(long transportId, String storyId, String nodeId, Kind kind, String optionId) {
        CanonicalSessionNetworkCodec.requirePositive(transportId, "transport_id");
        CanonicalSessionNetworkCodec.requireField(storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec.requireField(nodeId, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        if (kind == null) throw CanonicalSessionNetworkCodec.invalid("action kind is required");
        if (kind == Kind.CHOICE)
            CanonicalSessionNetworkCodec.requireField(optionId, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        else if (optionId != null && !optionId.trim()
            .isEmpty()) throw CanonicalSessionNetworkCodec.invalid("CONTINUE cannot carry option_id");
    }
}
