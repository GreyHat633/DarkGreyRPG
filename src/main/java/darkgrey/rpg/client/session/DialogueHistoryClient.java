package darkgrey.rpg.client.session;

import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

public final class DialogueHistoryClient {

    public static final DialogueBacklog HISTORY = new DialogueBacklog();
    private static darkgrey.rpg.network.message.canonical.CanonicalSessionAction pending;
    private static String pendingText;
    private static String pendingSpeaker;
    private static String pendingContext;
    private static String pendingIdentity;
    private static String lastPresentedIdentity, lastPresentedContext;

    private DialogueHistoryClient() {}

    public static String identity(CanonicalSessionFrame frame) {
        // transportId is a persisted, monotonically allocated Session instance id in this runtime.
        return frame.getStoryId() + "/"
            + frame.getSessionResourceId()
            + "/"
            + frame.getTransportId()
            + "/"
            + frame.getCurrentNodeId()
            + "/"
            + frame.getLineEpoch();
    }

    public static void presented(CanonicalSessionFrame frame, String speaker) {
        if (frame.getKind() != CanonicalSessionFrame.Kind.LINE) return;
        String context = PlayerReadingContext.current(), identity = identity(frame);
        if (context == null || context.equals(lastPresentedContext) && identity.equals(lastPresentedIdentity)) return;
        HISTORY.upsert(context, identity, speaker, frame.getText());
        lastPresentedContext = context;
        lastPresentedIdentity = identity;
    }

    public static void choosing(CanonicalSessionFrame frame,
        darkgrey.rpg.network.message.canonical.CanonicalSessionAction action) {
        for (darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption option : frame.getChoices()) {
            if (!option.getOptionId()
                .equals(action.getOptionId())) continue;
            pending = action;
            pendingText = option.getDisplayText();
            pendingSpeaker = net.minecraft.client.Minecraft.getMinecraft()
                .getSession()
                .getUsername();
            pendingContext = PlayerReadingContext.current();
            pendingIdentity = identity(frame) + "/choice";
            return;
        }
    }

    public static void acceptChoice(darkgrey.rpg.network.message.canonical.CanonicalSessionAction action) {
        if (pending == null || pending.getTransportId() != action.getTransportId()
            || pending.getLineEpoch() != action.getLineEpoch()
            || !pending.getStoryId()
                .equals(action.getStoryId())
            || !pending.getCurrentNodeId()
                .equals(action.getCurrentNodeId())
            || !pending.getOptionId()
                .equals(action.getOptionId())
            || !java.util.Objects.equals(pendingContext, PlayerReadingContext.current())) return;
        HISTORY.upsert(pendingContext, pendingIdentity, pendingSpeaker, pendingText);
        clearPending();
    }

    public static void clearPending() {
        pending = null;
        pendingText = null;
        pendingSpeaker = null;
        pendingContext = null;
        pendingIdentity = null;
    }
}
