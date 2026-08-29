package darkgrey.rpg.network.message.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Set;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import io.netty.buffer.ByteBuf;

/** Author-facing Session frame. Server-only identity and Story outputs are intentionally absent. */
public final class CanonicalSessionFrame implements IMessage {

    public enum Kind {
        LINE,
        CHOICE,
        NARRATION
    }

    private long transportId;
    private String storyId;
    private String sessionResourceId;
    private String currentNodeId;
    private Kind kind;
    private String speaker;
    private String text;
    private List<CanonicalSessionChoiceOption> choices = Collections.emptyList();

    public CanonicalSessionFrame() {}

    public CanonicalSessionFrame(long transportId, String storyId, String sessionResourceId, String currentNodeId,
        Kind kind, String speaker, String text, List<CanonicalSessionChoiceOption> choices) {
        validate(transportId, storyId, sessionResourceId, currentNodeId, kind, speaker, text, choices);
        this.transportId = transportId;
        this.storyId = storyId;
        this.sessionResourceId = sessionResourceId;
        this.currentNodeId = currentNodeId;
        this.kind = kind;
        this.speaker = speaker;
        this.text = text;
        this.choices = detached(choices);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 8) throw CanonicalSessionNetworkCodec.invalid("truncated transport_id");
        long decodedTransportId = buffer.readLong();
        String decodedStoryId = CanonicalSessionNetworkCodec
            .readField(buffer, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        String decodedResourceId = CanonicalSessionNetworkCodec
            .readField(buffer, "session_resource_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        String decodedNodeId = CanonicalSessionNetworkCodec
            .readField(buffer, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        Kind decodedKind = Kind.values()[CanonicalSessionNetworkCodec
            .readEnum(buffer, Kind.values().length, "frame kind")];
        String decodedSpeaker = decodedKind == Kind.CHOICE || decodedKind == Kind.NARRATION
            ? CanonicalSessionNetworkCodec
                .readOptionalField(buffer, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES)
            : CanonicalSessionNetworkCodec.readField(buffer, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        String decodedText = CanonicalSessionNetworkCodec
            .readField(buffer, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        if (!buffer.isReadable()) throw CanonicalSessionNetworkCodec.invalid("truncated choice count");
        int count = buffer.readUnsignedByte();
        if (count > CanonicalSessionNetworkCodec.MAX_OPTIONS)
            throw CanonicalSessionNetworkCodec.invalid("too many choices");
        List<CanonicalSessionChoiceOption> decodedChoices = new ArrayList<CanonicalSessionChoiceOption>(count);
        Set<String> ids = CanonicalSessionNetworkCodec.newIdSet();
        for (int index = 0; index < count; index++) {
            String id = CanonicalSessionNetworkCodec
                .readField(buffer, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
            String display = CanonicalSessionNetworkCodec
                .readField(buffer, "display_text", CanonicalSessionNetworkCodec.MAX_OPTION_TEXT_BYTES);
            CanonicalSessionNetworkCodec.requireUnique(id, ids);
            decodedChoices.add(new CanonicalSessionChoiceOption(id, display));
        }
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        validate(
            decodedTransportId,
            decodedStoryId,
            decodedResourceId,
            decodedNodeId,
            decodedKind,
            decodedSpeaker,
            decodedText,
            decodedChoices);
        transportId = decodedTransportId;
        storyId = decodedStoryId;
        sessionResourceId = decodedResourceId;
        currentNodeId = decodedNodeId;
        kind = decodedKind;
        speaker = decodedSpeaker;
        text = decodedText;
        choices = detached(decodedChoices);
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(transportId, storyId, sessionResourceId, currentNodeId, kind, speaker, text, choices);
        buffer.writeLong(transportId);
        CanonicalSessionNetworkCodec.writeField(buffer, storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec
            .writeField(buffer, sessionResourceId, "session_resource_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec
            .writeField(buffer, currentNodeId, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        buffer.writeByte(kind.ordinal());
        if (kind == Kind.CHOICE || kind == Kind.NARRATION) {
            CanonicalSessionNetworkCodec
                .writeOptionalEmptyField(buffer, speaker, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        } else {
            CanonicalSessionNetworkCodec
                .writeField(buffer, speaker, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        }
        CanonicalSessionNetworkCodec.writeField(buffer, text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        buffer.writeByte(choices.size());
        for (CanonicalSessionChoiceOption choice : choices) {
            CanonicalSessionNetworkCodec
                .writeField(buffer, choice.getOptionId(), "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
            CanonicalSessionNetworkCodec.writeField(
                buffer,
                choice.getDisplayText(),
                "display_text",
                CanonicalSessionNetworkCodec.MAX_OPTION_TEXT_BYTES);
        }
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

    public String getSessionResourceId() {
        return sessionResourceId;
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

    public String getSpeaker() {
        return speaker;
    }

    public String getSpeakerName() {
        return speaker;
    }

    public String getText() {
        return text;
    }

    public List<CanonicalSessionChoiceOption> getChoices() {
        return choices;
    }

    public boolean canContinue() {
        return kind == Kind.LINE || kind == Kind.NARRATION;
    }

    private static List<CanonicalSessionChoiceOption> detached(List<CanonicalSessionChoiceOption> values) {
        return Collections.unmodifiableList(new ArrayList<CanonicalSessionChoiceOption>(values));
    }

    private static void validate(long transportId, String storyId, String resourceId, String nodeId, Kind kind,
        String speaker, String text, List<CanonicalSessionChoiceOption> choices) {
        CanonicalSessionNetworkCodec.requirePositive(transportId, "transport_id");
        CanonicalSessionNetworkCodec.requireField(storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec
            .requireField(resourceId, "session_resource_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec.requireField(nodeId, "current_node_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        if (kind == null) throw CanonicalSessionNetworkCodec.invalid("frame kind is required");
        if (kind == Kind.LINE) {
            CanonicalSessionNetworkCodec
                .requireField(speaker, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        } else if (speaker == null || !speaker.isEmpty()) {
            throw CanonicalSessionNetworkCodec.invalid(kind.name() + " speaker must be explicitly empty");
        }
        CanonicalSessionNetworkCodec.requireField(text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        if (choices == null || choices.size() > CanonicalSessionNetworkCodec.MAX_OPTIONS)
            throw CanonicalSessionNetworkCodec.invalid("invalid choice count");
        Set<String> ids = CanonicalSessionNetworkCodec.newIdSet();
        for (CanonicalSessionChoiceOption choice : choices) {
            if (choice == null) throw CanonicalSessionNetworkCodec.invalid("null choice");
            CanonicalSessionNetworkCodec.requireUnique(choice.getOptionId(), ids);
        }
        if ((kind == Kind.LINE || kind == Kind.NARRATION) && !choices.isEmpty())
            throw CanonicalSessionNetworkCodec.invalid(kind.name() + " cannot carry choices");
        if (kind == Kind.CHOICE && choices.isEmpty())
            throw CanonicalSessionNetworkCodec.invalid("CHOICE requires choices");
    }
}
