package darkgrey.rpg.client.session;

import java.util.Arrays;
import java.util.Collections;

import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Focused client identity-fence and stable-option probe; no Minecraft process is required. */
public final class CanonicalSessionClientModelProbe {

    private CanonicalSessionClientModelProbe() {}

    public static void main(String[] args) {
        CanonicalSessionClientModel model = new CanonicalSessionClientModel();
        CanonicalSessionFrame line = line(11L, "story_a", "node_line");
        require(model.acceptFrame(line), "new frame accepted");
        require(
            model.continueAction()
                .getCurrentNodeId()
                .equals("node_line"),
            "continue uses current node");
        ByteBuf changedBytes = Unpooled.buffer();
        line(11L, "story_a", "node_changed").toBytes(changedBytes);
        line.fromBytes(changedBytes);
        changedBytes.release();
        require("node_line".equals(model.getCurrentNodeId()), "model detaches incoming frame");
        require(!model.acceptFrame(line(12L, "story_a", "node_stale")), "transport fence");
        require(!model.acceptFrame(line(11L, "story_b", "node_other")), "Story fence");

        CanonicalSessionFrame choice = new CanonicalSessionFrame(
            11L,
            "story_a",
            "session_a",
            "node_choice",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "Pick",
            Arrays.asList(
                new CanonicalSessionChoiceOption("option_alpha", "Alpha"),
                new CanonicalSessionChoiceOption("option_omega", "Omega")));
        require(model.acceptFrame(choice), "matching choice accepted");
        CanonicalSessionAction action = model.choiceAction("option_omega");
        require("option_omega".equals(action.getOptionId()), "stable option action");
        require("node_choice".equals(action.getCurrentNodeId()), "choice current node");
        reject(new Runnable() {

            @Override
            public void run() {
                model.choiceAction("1");
            }
        }, "array index rejected");
        require(!model.acceptClose(new CanonicalSessionClose(12L, "story_a")), "stale close rejected");
        require(model.acceptClose(new CanonicalSessionClose(11L, "story_a")), "matching close accepted");
        require(!model.isActive(), "close clears state");
        require(model.acceptFrame(line(7L, "story_new", "new_line")), "new identity accepted");
        System.out.println("CANONICAL_SESSION_CLIENT_MODEL_PROBE=PASS");
    }

    private static CanonicalSessionFrame line(long transportId, String storyId, String nodeId) {
        return new CanonicalSessionFrame(
            transportId,
            storyId,
            "session_a",
            nodeId,
            CanonicalSessionFrame.Kind.LINE,
            "Speaker",
            "Text",
            Collections.<CanonicalSessionChoiceOption>emptyList());
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
            throw new IllegalStateException("Probe failure: " + label);
        } catch (IllegalArgumentException expected) {
            // expected
        }
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
