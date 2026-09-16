package darkgrey.rpg.client.session;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Client-only state model guarded by canonical transport and Story identity. */
public final class CanonicalSessionClientModel {

    private CanonicalSessionFrame frame;
    private String visibleText = "";
    private final DialogueTextReveal reveal = new DialogueTextReveal();
    private String visibleSpeaker = "";
    /** Portrait context for the currently visible line, retained while a choice frame is shown. */
    private String visiblePortraitRef;

    public synchronized void clear() {
        frame = null;
        visibleText = "";
        visibleSpeaker = "";
        visiblePortraitRef = null;
    }

    public synchronized boolean acceptFrame(CanonicalSessionFrame update) {
        if (update == null) return false;
        if (frame != null && (frame.getTransportId() != update.getTransportId() || !frame.getStoryId()
            .equals(update.getStoryId())
            || !frame.getSessionResourceId()
                .equals(update.getSessionResourceId())))
            return false;
        if (frame != null && (update.getLineEpoch() < frame.getLineEpoch() || update.getPresentation()
            .getRevision()
            < frame.getPresentation()
                .getRevision()))
            return false;
        boolean newLine = update.getKind() == CanonicalSessionFrame.Kind.LINE
            && (frame == null || frame.getLineEpoch() != update.getLineEpoch()
                || !frame.getCurrentNodeId()
                    .equals(update.getCurrentNodeId()));
        if (update.getKind() == CanonicalSessionFrame.Kind.LINE || frame == null) {
            visibleText = update.getText();
            visibleSpeaker = update.getSpeaker();
        }
        // Choice packets intentionally carry no line media. Keep the current portrait
        // visible until the next line explicitly replaces it.
        if (update.getKind() == CanonicalSessionFrame.Kind.LINE) visiblePortraitRef = update.getPortraitRef();
        if (newLine) reveal.begin(visibleText, DialoguePreferences.resolve(update.getTextSpeed()), System.nanoTime());
        else if (update.getKind() == CanonicalSessionFrame.Kind.CHOICE) {
            reveal.begin(visibleText, 0, System.nanoTime());
        }
        frame = copy(update);
        return true;
    }

    public synchronized boolean applyFrame(CanonicalSessionFrame update) {
        return acceptFrame(update);
    }

    public synchronized boolean acceptClose(CanonicalSessionClose close) {
        if (frame == null || close == null
            || frame.getTransportId() != close.getTransportId()
            || !frame.getStoryId()
                .equals(close.getStoryId()))
            return false;
        frame = null;
        visibleText = "";
        visibleSpeaker = "";
        visiblePortraitRef = null;
        return true;
    }

    public synchronized boolean applyClose(CanonicalSessionClose close) {
        return acceptClose(close);
    }

    public synchronized boolean isActive() {
        return frame != null;
    }

    public synchronized CanonicalSessionFrame getFrame() {
        return frame == null ? null : copy(frame);
    }

    public synchronized long getTransportId() {
        requireActive();
        return frame.getTransportId();
    }

    public synchronized long getSessionId() {
        return getTransportId();
    }

    public synchronized String getStoryId() {
        requireActive();
        return frame.getStoryId();
    }

    public synchronized String getCurrentNodeId() {
        requireActive();
        return frame.getCurrentNodeId();
    }

    public synchronized CanonicalSessionFrame.Kind getKind() {
        requireActive();
        return frame.getKind();
    }

    public synchronized String getSpeaker() {
        requireActive();
        return frame.getSpeaker();
    }

    public synchronized String getText() {
        requireActive();
        return frame.getText();
    }

    /** Presentation context only; the authoritative frame and outgoing actions stay unchanged. */
    public synchronized String getVisibleText() {
        return frame == null ? "" : reveal.visible(System.nanoTime());
    }

    public synchronized boolean finishVisibleText() {
        return frame != null && frame.getKind() == CanonicalSessionFrame.Kind.LINE && reveal.finish(System.nanoTime());
    }

    public synchronized String getVisibleSpeaker() {
        return visibleSpeaker;
    }

    /**
     * Returns the portrait belonging to the visible line. Choice frames intentionally omit
     * line media, so this context survives while the options are displayed.
     */
    public synchronized String getVisiblePortraitRef() {
        return visiblePortraitRef;
    }

    public synchronized List<CanonicalSessionChoiceOption> getChoices() {
        requireActive();
        return Collections.unmodifiableList(new ArrayList<CanonicalSessionChoiceOption>(frame.getChoices()));
    }

    public synchronized CanonicalSessionAction continueAction() {
        requireActive();
        if (!frame.canContinue()) throw new IllegalStateException("Canonical Session is not on a Line.");
        return new CanonicalSessionAction(
            frame.getTransportId(),
            frame.getStoryId(),
            frame.getCurrentNodeId(),
            CanonicalSessionAction.Kind.CONTINUE,
            null);
    }

    public synchronized CanonicalSessionAction choiceAction(String optionId) {
        requireActive();
        if (frame.getKind() != CanonicalSessionFrame.Kind.CHOICE)
            throw new IllegalStateException("Canonical Session is not on a Choice.");
        for (CanonicalSessionChoiceOption choice : frame.getChoices()) {
            if (choice.getOptionId()
                .equals(optionId))
                return new CanonicalSessionAction(
                    frame.getTransportId(),
                    frame.getStoryId(),
                    frame.getCurrentNodeId(),
                    CanonicalSessionAction.Kind.CHOICE,
                    optionId);
        }
        throw new IllegalArgumentException("Unknown canonical Session option ID.");
    }

    private void requireActive() {
        if (frame == null) throw new IllegalStateException("No active canonical Session.");
    }

    private static CanonicalSessionFrame copy(CanonicalSessionFrame source) {
        return new CanonicalSessionFrame(
            source.getTransportId(),
            source.getStoryId(),
            source.getSessionResourceId(),
            source.getCurrentNodeId(),
            source.getKind(),
            source.getSpeaker(),
            source.getText(),
            source.getChoices(),
            source.getPortraitRef(),
            source.getVoiceRef(),
            source.getVoiceVolume()).withTextSpeed(source.getTextSpeed())
                .withPresentation(source.getPresentation(), source.getLineEpoch(), source.shouldPlayVoice());
    }
}
