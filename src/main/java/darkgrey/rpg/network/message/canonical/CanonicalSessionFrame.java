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
        PRESENTATION
    }

    private darkgrey.rpg.session.runtime.CanonicalSessionPresentation presentation = darkgrey.rpg.session.runtime.CanonicalSessionPresentation.EMPTY;
    private long lineEpoch;
    private boolean playVoice;

    public darkgrey.rpg.session.runtime.CanonicalSessionPresentation getPresentation() {
        return presentation;
    }

    public long getLineEpoch() {
        return lineEpoch;
    }

    public boolean shouldPlayVoice() {
        return playVoice;
    }

    public CanonicalSessionFrame withPresentation(darkgrey.rpg.session.runtime.CanonicalSessionPresentation state,
        long epoch, boolean voice) {
        if (state == null || epoch < 0) throw CanonicalSessionNetworkCodec.invalid("presentation state");
        CanonicalSessionFrame copy = new CanonicalSessionFrame(
            transportId,
            storyId,
            sessionResourceId,
            currentNodeId,
            kind,
            speaker,
            text,
            choices,
            portraitRef,
            voiceRef);
        copy.presentation = state;
        copy.lineEpoch = epoch;
        copy.playVoice = voice && kind == Kind.LINE && voiceRef != null;
        return copy;
    }

    private String portraitRef;
    private String voiceRef;
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
        this(transportId, storyId, sessionResourceId, currentNodeId, kind, speaker, text, choices, null, null);
    }

    public CanonicalSessionFrame(long transportId, String storyId, String sessionResourceId, String currentNodeId,
        Kind kind, String speaker, String text, List<CanonicalSessionChoiceOption> choices, String portraitRef,
        String voiceRef) {
        validate(transportId, storyId, sessionResourceId, currentNodeId, kind, speaker, text, choices);
        validateMedia(kind, speaker, portraitRef, voiceRef);
        this.portraitRef = portraitRef;
        this.voiceRef = voiceRef;
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
        String decodedSpeaker = CanonicalSessionNetworkCodec
            .readOptionalField(buffer, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        String decodedText = decodedKind != Kind.LINE
            ? CanonicalSessionNetworkCodec
                .readOptionalField(buffer, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES)
            : CanonicalSessionNetworkCodec.readField(buffer, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
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
        String decodedPortrait = CanonicalSessionNetworkCodec.readOptionalField(buffer, "portrait_ref", 80);
        String decodedVoice = CanonicalSessionNetworkCodec.readOptionalField(buffer, "voice_ref", 80);
        decodedPortrait = decodedPortrait.isEmpty() ? null : decodedPortrait;
        decodedVoice = decodedVoice.isEmpty() ? null : decodedVoice;
        validateMedia(decodedKind, decodedSpeaker, decodedPortrait, decodedVoice);
        darkgrey.rpg.session.runtime.CanonicalSessionPresentation decodedPresentation = darkgrey.rpg.session.runtime.CanonicalSessionPresentation
            .fromJson(CanonicalSessionNetworkCodec.readField(buffer, "presentation", 16384));
        if (buffer.readableBytes() < 9) throw CanonicalSessionNetworkCodec.invalid("truncated presentation state");
        long decodedEpoch = buffer.readLong();
        int decodedPlay = buffer.readUnsignedByte();
        if (decodedEpoch < 0 || decodedPlay > 1
            || (decodedPlay == 1 && (decodedKind != Kind.LINE || decodedVoice == null)))
            throw CanonicalSessionNetworkCodec.invalid("invalid voice state");
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        presentation = decodedPresentation;
        lineEpoch = decodedEpoch;
        playVoice = decodedPlay == 1;
        validate(
            decodedTransportId,
            decodedStoryId,
            decodedResourceId,
            decodedNodeId,
            decodedKind,
            decodedSpeaker,
            decodedText,
            decodedChoices);
        portraitRef = decodedPortrait;
        voiceRef = decodedVoice;
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
        CanonicalSessionNetworkCodec
            .writeOptionalField(buffer, speaker, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        if (kind != Kind.LINE) {
            CanonicalSessionNetworkCodec
                .writeOptionalField(buffer, text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        } else {
            CanonicalSessionNetworkCodec.writeField(buffer, text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        }
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
        validateMedia(kind, speaker, portraitRef, voiceRef);
        CanonicalSessionNetworkCodec
            .writeOptionalField(buffer, portraitRef == null ? "" : portraitRef, "portrait_ref", 80);
        CanonicalSessionNetworkCodec.writeOptionalField(buffer, voiceRef == null ? "" : voiceRef, "voice_ref", 80);
        CanonicalSessionNetworkCodec.writeField(buffer, presentation.toJson(), "presentation", 16384);
        buffer.writeLong(lineEpoch);
        buffer.writeBoolean(playVoice);
    }

    public String getPortraitRef() {
        return portraitRef;
    }

    public String getVoiceRef() {
        return voiceRef;
    }

    private static void validateMedia(Kind kind, String speaker, String portrait, String voice) {
        if (kind != Kind.LINE && (portrait != null || voice != null))
            throw CanonicalSessionNetworkCodec.invalid("Only LINE can carry portrait or voice media");
        if (portrait != null && (speaker == null || speaker.isEmpty()
            || !darkgrey.rpg.graph.canonical.CanonicalMediaReference.isImage(portrait)))
            throw CanonicalSessionNetworkCodec.invalid("Invalid line portrait");
        if (voice != null && !darkgrey.rpg.graph.canonical.CanonicalMediaReference.isAudio(voice))
            throw CanonicalSessionNetworkCodec.invalid("Invalid line voice");
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
        return kind == Kind.LINE;
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
                .requireOptionalField(speaker, "speaker", CanonicalSessionNetworkCodec.MAX_SPEAKER_BYTES);
        } else if (speaker == null || !speaker.isEmpty()) {
            throw CanonicalSessionNetworkCodec.invalid(kind.name() + " speaker must be explicitly empty");
        }
        if (kind != Kind.LINE) {
            CanonicalSessionNetworkCodec
                .requireOptionalField(text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        } else {
            CanonicalSessionNetworkCodec.requireField(text, "text", CanonicalSessionNetworkCodec.MAX_TEXT_BYTES);
        }
        if (kind == Kind.PRESENTATION && !text.isEmpty())
            throw CanonicalSessionNetworkCodec.invalid("presentation text must be empty");
        if (choices == null || choices.size() > CanonicalSessionNetworkCodec.MAX_OPTIONS)
            throw CanonicalSessionNetworkCodec.invalid("invalid choice count");
        Set<String> ids = CanonicalSessionNetworkCodec.newIdSet();
        for (CanonicalSessionChoiceOption choice : choices) {
            if (choice == null) throw CanonicalSessionNetworkCodec.invalid("null choice");
            CanonicalSessionNetworkCodec.requireUnique(choice.getOptionId(), ids);
        }
        if (kind != Kind.CHOICE && !choices.isEmpty())
            throw CanonicalSessionNetworkCodec.invalid(kind.name() + " cannot carry choices");
        if (kind == Kind.CHOICE && choices.isEmpty())
            throw CanonicalSessionNetworkCodec.invalid("CHOICE requires choices");
    }
}
