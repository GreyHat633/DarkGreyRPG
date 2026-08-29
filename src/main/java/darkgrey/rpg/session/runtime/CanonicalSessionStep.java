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

    private final Kind kind;
    private final String nodeId;
    private final String speakerActorId;
    private final String text;
    private final String prompt;
    private final List<CanonicalSessionChoiceOption> options;
    private final String endPortId;
    private final String endDisplayName;

    private CanonicalSessionStep(Kind kind, String nodeId, String speakerActorId, String text, String prompt,
        List<CanonicalSessionChoiceOption> options, String endPortId, String endDisplayName) {
        this.kind = kind;
        this.nodeId = nodeId;
        this.speakerActorId = speakerActorId;
        this.text = text;
        this.prompt = prompt;
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
            null,
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            null,
            null);
    }

    public static CanonicalSessionStep choice(String nodeId, String prompt,
        List<CanonicalSessionChoiceOption> options) {
        return new CanonicalSessionStep(Kind.CHOICE, nodeId, null, null, prompt, options, null, null);
    }

    public static CanonicalSessionStep end(String nodeId, String endPortId, String endDisplayName) {
        return new CanonicalSessionStep(
            Kind.END,
            nodeId,
            null,
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

    public String getPrompt() {
        return prompt;
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
