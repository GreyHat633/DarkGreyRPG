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
    private long closedTransport = -1;
    private String visibleText = "";
    private final DialogueTextReveal reveal = new DialogueTextReveal();
    private String visibleSpeaker = "";
    /** Portrait context for the currently visible line, retained while a choice frame is shown. */
    private String visiblePortraitRef;
    private DialogueDisplayPages displayPages;
    private String layoutKey;
    private int displayPage;
    private boolean awaiting;
    private long autoStarted;

    public synchronized void clear() {
        frame = null;
        closedTransport = -1;
        visibleText = "";
        visibleSpeaker = "";
        visiblePortraitRef = null;
        displayPages = null;
        layoutKey = null;
        displayPage = 0;
        awaiting = false;
        autoStarted = 0;
    }

    public synchronized boolean reject(long transport, String story) {
        if (frame == null || frame.getTransportId() != transport
            || !frame.getStoryId()
                .equals(story))
            return false;
        awaiting = false;
        autoStarted = 0;
        return true;
    }

    public synchronized boolean acceptFrame(CanonicalSessionFrame update) {
        if (update == null || update.getTransportId() <= closedTransport) return false;
        if (frame != null && (frame.getTransportId() != update.getTransportId() || !frame.getStoryId()
            .equals(update.getStoryId())
            || !frame.getSessionResourceId()
                .equals(update.getSessionResourceId())))
            return false;
        if (frame != null && (update.getProjectionRevision() < frame.getProjectionRevision()
            || update.getLineEpoch() < frame.getLineEpoch()
            || update.getPresentation()
                .getRevision()
                < frame.getPresentation()
                    .getRevision()))
            return false;
        boolean newLine = update.getKind() == CanonicalSessionFrame.Kind.LINE
            && (frame == null || frame.getLineEpoch() != update.getLineEpoch()
                || frame.getKind() != CanonicalSessionFrame.Kind.LINE
                || !frame.getCurrentNodeId()
                    .equals(update.getCurrentNodeId()));
        boolean newChoice = update.getKind() == CanonicalSessionFrame.Kind.CHOICE
            && (frame == null || frame.getKind() != CanonicalSessionFrame.Kind.CHOICE
                || frame.getLineEpoch() != update.getLineEpoch()
                || !frame.getCurrentNodeId()
                    .equals(update.getCurrentNodeId()));
        if (update.getKind() == CanonicalSessionFrame.Kind.LINE || frame == null) {
            visibleText = update.getText();
            visibleSpeaker = update.getSpeaker();
        }
        // Choice packets intentionally carry no line media. Keep the current portrait
        // visible until the next line explicitly replaces it.
        if (update.getKind() == CanonicalSessionFrame.Kind.LINE) visiblePortraitRef = update.getPortraitRef();
        if (newLine) {
            displayPages = null;
            layoutKey = null;
            displayPage = 0;
            awaiting = false;
            autoStarted = 0;
            reveal.begin(visibleText, DialoguePreferences.resolve(update.getTextSpeed()), System.nanoTime());
        } else if (newChoice) {
            awaiting = false;
            autoStarted = 0;
            if (displayPages != null) {
                reveal.begin(
                    visibleText.substring(displayPages.start(displayPage), displayPages.end(displayPage)),
                    0,
                    System.nanoTime());
            } else reveal.begin(visibleText, 0, System.nanoTime());
        }
        frame = copy(update);
        if (update.getKind() == CanonicalSessionFrame.Kind.CHOICE) awaiting = false;
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
        long retired = Math.max(closedTransport, close.getTransportId());
        clear();
        closedTransport = retired;
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
        if (frame != null && frame.getKind() == CanonicalSessionFrame.Kind.LINE)
            reveal.speed(DialoguePreferences.resolve(frame.getTextSpeed()), System.nanoTime());
        return frame == null ? "" : reveal.visible(System.nanoTime());
    }

    public synchronized void layout(String key, double width, int rows, DialogueDisplayPages.Metrics metrics) {
        if (frame == null || key.equals(layoutKey)) return;
        int start = displayPages == null
            ? (frame.getKind() == CanonicalSessionFrame.Kind.CHOICE ? visibleText.length() : 0)
            : displayPages.start(displayPage);
        int shown = start + getVisibleText().length();
        displayPages = DialogueDisplayPages.measure(visibleText, width, rows, metrics);
        displayPage = displayPages.pageAt(start);
        layoutKey = key;
        beginDisplayPage();
        reveal.retain(shown - displayPages.start(displayPage));
    }

    private void beginDisplayPage() {
        reveal.begin(
            visibleText.substring(displayPages.start(displayPage), displayPages.end(displayPage)),
            frame.getKind() == CanonicalSessionFrame.Kind.LINE ? DialoguePreferences.resolve(frame.getTextSpeed()) : 0,
            System.nanoTime());
        autoStarted = 0;
    }

    public synchronized List<String> getDisplayLines() {
        List<String> result = new ArrayList<String>();
        if (displayPages == null) {
            result.add(getVisibleText());
            return result;
        }
        int shown = displayPages.start(displayPage) + getVisibleText().length();
        int first = displayPage * displayPages.linesPerPage;
        for (int i = first; i < Math.min(displayPages.lines.size(), first + displayPages.linesPerPage); i++) {
            DialogueDisplayPages.Line line = displayPages.lines.get(i);
            result.add(
                line.format + visibleText.substring(line.start, Math.max(line.start, Math.min(line.end, shown)))
                    .replace("\r", "")
                    .replace("\n", ""));
        }
        return result;
    }

    /** True means this advance was handled locally; no network/audio action is permitted. */
    public synchronized boolean advanceDisplayPage() {
        autoStarted = 0;
        if (awaiting || frame == null || !frame.canContinue()) return true;
        if (finishVisibleText()) return true;
        if (displayPages != null && displayPage + 1 < displayPages.pageCount()) {
            displayPage++;
            beginDisplayPage();
            return true;
        }
        awaiting = true;
        return false;
    }

    public synchronized boolean displayTextComplete() {
        if (frame == null) return false;
        int length = displayPages == null ? visibleText.length()
            : displayPages.end(displayPage) - displayPages.start(displayPage);
        return getVisibleText().length() >= length;
    }

    public synchronized boolean awaitingServer() {
        return awaiting;
    }

    public synchronized void awaitChoice() {
        awaiting = true;
        autoStarted = 0;
    }

    public synchronized boolean automatic() {
        return PlayerUiPreferences.automatic();
    }

    public synchronized void toggleAutomatic() {
        PlayerUiPreferences.setAutomatic(!PlayerUiPreferences.automatic());
        autoStarted = 0;
    }

    public synchronized void pauseAutomatic() {
        autoStarted = 0;
    }

    public synchronized boolean autoDue(boolean foreground, long now) {
        if (!foreground || !automatic()
            || awaiting
            || frame == null
            || !frame.canContinue()
            || displayPages == null
            || getVisibleText().length() < displayPages.end(displayPage) - displayPages.start(displayPage)) {
            autoStarted = 0;
            return false;
        }
        if (autoStarted == 0) autoStarted = now;
        if (now - autoStarted < PlayerUiPreferences.autoWaitSeconds() * 1000000000.0) return false;
        autoStarted = 0;
        return true;
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
            null,
            frame.getLineEpoch());
    }

    public synchronized CanonicalSessionAction choiceAction(String optionId) {
        requireActive();
        if (frame.getKind() != CanonicalSessionFrame.Kind.CHOICE)
            throw new IllegalStateException("Canonical Session is not on a Choice.");
        for (CanonicalSessionChoiceOption choice : frame.getChoices()) {
            if (!choice.isEnabled()) continue;
            if (choice.getOptionId()
                .equals(optionId))
                return new CanonicalSessionAction(
                    frame.getTransportId(),
                    frame.getStoryId(),
                    frame.getCurrentNodeId(),
                    CanonicalSessionAction.Kind.CHOICE,
                    optionId,
                    frame.getLineEpoch());
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
                .withPresentation(source.getPresentation(), source.getLineEpoch(), source.shouldPlayVoice())
                .withScreenPlayback(source.shouldPlayScreen())
                .withProjectionRevision(source.getProjectionRevision());
    }
}
