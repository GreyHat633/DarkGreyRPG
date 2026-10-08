package darkgrey.rpg.session.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Detached view of the current executable Session node. */
public final class CanonicalSessionStep {

    public enum Kind {
        LINE,
        CHOICE,
        END
    }

    private final String portraitVariant;
    private final String voiceRef;
    private final double voiceVolume;
    private double textSpeed = 30;

    public double getTextSpeed() {
        return textSpeed;
    }

    public CanonicalSessionStep withTextSpeed(double value) {
        if (Double.isNaN(value) || Double.isInfinite(value) || (value != -1 && value < 0) || value > 120)
            throw new IllegalArgumentException("Invalid text speed");
        textSpeed = value;
        return this;
    }

    private final Kind kind;
    private final String nodeId;
    private final String speakerActorId;
    private final String text;
    private final List<CanonicalSessionChoiceOption> options;
    private final String endPortId;
    private final String endDisplayName;

    private CanonicalSessionStep(Kind kind, String nodeId, String speakerActorId, String text,
        List<CanonicalSessionChoiceOption> options, String endPortId, String endDisplayName) {
        this(kind, nodeId, speakerActorId, text, options, endPortId, endDisplayName, null, null, 1);
    }

    private CanonicalSessionStep(Kind kind, String nodeId, String speakerActorId, String text,
        List<CanonicalSessionChoiceOption> options, String endPortId, String endDisplayName, String portraitVariant,
        String voiceRef, double voiceVolume) {
        this.portraitVariant = portraitVariant;
        this.voiceRef = voiceRef;
        this.voiceVolume = voiceVolume;
        this.kind = kind;
        this.nodeId = nodeId;
        this.speakerActorId = speakerActorId;
        this.text = text;
        this.options = Collections.unmodifiableList(new ArrayList<CanonicalSessionChoiceOption>(options));
        this.endPortId = endPortId;
        this.endDisplayName = endDisplayName;
    }

    public static CanonicalSessionStep line(String nodeId, String speakerActorId, String text) {
        return new CanonicalSessionStep(
            Kind.LINE,
            nodeId,
            speakerActorId,
            text,
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            null,
            null);
    }

    public static CanonicalSessionStep line(String nodeId, String speakerActorId, String text, String portraitVariant,
        String voiceRef) {
        return line(nodeId, speakerActorId, text, portraitVariant, voiceRef, 1);
    }

    public static CanonicalSessionStep line(String nodeId, String speakerActorId, String text, String portraitVariant,
        String voiceRef, double voiceVolume) {
        return new CanonicalSessionStep(
            Kind.LINE,
            nodeId,
            speakerActorId,
            text,
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            null,
            null,
            portraitVariant,
            voiceRef,
            voiceVolume);
    }

    public String getPortraitVariant() {
        return portraitVariant;
    }

    public String getVoiceRef() {
        return voiceRef;
    }

    public double getVoiceVolume() {
        return voiceVolume;
    }

    public static CanonicalSessionStep choice(String nodeId, List<CanonicalSessionChoiceOption> options) {
        return new CanonicalSessionStep(Kind.CHOICE, nodeId, null, null, options, null, null);
    }

    public static CanonicalSessionStep end(String nodeId, String endPortId, String endDisplayName) {
        return new CanonicalSessionStep(
            Kind.END,
            nodeId,
            null,
            null,
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            endPortId,
            endDisplayName);
    }

    public Kind getKind() {
        return kind;
    }

    public String getNodeId() {
        return nodeId;
    }

    public String getSpeakerActorId() {
        return speakerActorId;
    }

    public String getText() {
        return text;
    }

    public List<CanonicalSessionChoiceOption> getOptions() {
        return options;
    }

    public String getEndPortId() {
        return endPortId;
    }

    public String getEndDisplayName() {
        return endDisplayName;
    }
}
