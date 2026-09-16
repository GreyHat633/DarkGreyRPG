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
        DialoguePreferences.setSpeed(60);
        require(DialoguePreferences.resolve(-1) == 60, "global speed selected");
        require(DialoguePreferences.resolve(0) == 0, "zero override stays immediate");
        require(DialoguePreferences.resolve(120) == 120, "custom override wins");
        CanonicalSessionFrame inherited = line(10L, "story_a", "node_inherit").withTextSpeed(-1);
        ByteBuf inheritedBytes = Unpooled.buffer();
        inherited.toBytes(inheritedBytes);
        CanonicalSessionFrame decodedInherited = new CanonicalSessionFrame();
        decodedInherited.fromBytes(inheritedBytes);
        inheritedBytes.release();
        require(decodedInherited.getTextSpeed() == -1, "global speed sentinel survives network");
        DialoguePreferences.setSpeed(30);
        DialogueTextReveal reveal = new DialogueTextReveal();
        reveal.begin("中😀文\n末", 30, 0);
        require(
            reveal.visible(0)
                .equals(""),
            "starts empty");
        require(
            reveal.visible(70000000L)
                .equals("中😀"),
            "elapsed time and surrogate boundary");
        require(reveal.finish(70000000L), "first click completes");
        require(!reveal.finish(70000000L), "second click can advance");
        reveal.begin("立即", 0, 0);
        require(
            reveal.visible(0)
                .equals("立即"),
            "zero means immediate");
        reveal.begin("123456", 120, 0);
        require(
            reveal.visible(25000000L)
                .equals("123"),
            "120 characters per second");
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
        CanonicalSessionFrame portraitLine = lineWithPortrait(11L, "story_a", "node_portrait");
        require(model.acceptFrame(portraitLine), "portrait line accepted");
        require(
            "media/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png"
                .equals(model.getVisiblePortraitRef()),
            "line portrait becomes visible context");
        require(!model.acceptFrame(line(12L, "story_a", "node_stale")), "transport fence");
        require(!model.acceptFrame(line(11L, "story_b", "node_other")), "Story fence");

        CanonicalSessionFrame emptyChoice = new CanonicalSessionFrame(
            11L,
            "story_a",
            "session_a",
            "empty_choice",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "",
            Arrays.asList(new CanonicalSessionChoiceOption("yes", "是")));
        require(model.acceptFrame(emptyChoice), "promptless choice accepted");
        require("Text".equals(model.getVisibleText()), "promptless choice preserves visible line");
        require("Speaker".equals(model.getVisibleSpeaker()), "promptless choice preserves speaker");
        require(
            "media/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png"
                .equals(model.getVisiblePortraitRef()),
            "promptless choice preserves portrait context");
        require(
            model.getText()
                .isEmpty(),
            "wire frame remains promptless");
        require(
            "empty_choice".equals(
                model.choiceAction("yes")
                    .getCurrentNodeId()),
            "retained text cannot replace action identity");
        require(!model.acceptFrame(line(12L, "story_a", "stale")), "reject foreign context");
        require("Text".equals(model.getVisibleText()), "foreign frame cannot overwrite context");

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
        require("Text".equals(model.getVisibleText()), "choice preserves complete previous line");
        require(
            model.getVisibleSpeaker()
                .equals("Speaker"),
            "choice preserves previous speaker");
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
        require(
            model.getVisibleText()
                .isEmpty()
                && model.getVisibleSpeaker()
                    .isEmpty()
                && model.getVisiblePortraitRef() == null,
            "close clears presentation context");
        require(
            model.acceptFrame(
                new CanonicalSessionFrame(
                    7L,
                    "story_new",
                    "session_a",
                    "choice",
                    CanonicalSessionFrame.Kind.CHOICE,
                    "",
                    "",
                    Arrays.asList(new CanonicalSessionChoiceOption("yes", "Yes")))),
            "new choice accepted");
        require(
            model.getVisibleText()
                .isEmpty(),
            "new session cannot inherit old text");
        require(model.acceptClose(new CanonicalSessionClose(7L, "story_new")), "new close");
        for (int[] size : new int[][] { { 320, 240 }, { 427, 240 }, { 640, 360 }, { 960, 540 }, { 1920, 1080 } }) {
            CanonicalDialogueLayout layout = new CanonicalDialogueLayout(size[0], size[1]);
            require(layout.left == size[0] / 20, "five percent horizontal safe margin");
            require(layout.top > size[1] / 2 && layout.bottom < size[1], "bottom dialogue");
            require(layout.textWidth > 0, "positive text width");
            require(
                layout.choiceTop(layout.choicesPerPage) + layout.choicesPerPage * 24 + 20 < layout.top,
                "choices and paging never overlap dialogue");
            require(layout.containsDialogue(layout.left, layout.top), "dialogue edge clickable");
            require(!layout.containsDialogue(layout.right, layout.top), "right outside excluded");
            require(!layout.containsDialogue(size[0] / 2, layout.top - 1), "world click excluded");
        }
        System.out.println("CANONICAL_DIALOGUE_PRESENTATION_PROBE=PASS");
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

    private static CanonicalSessionFrame lineWithPortrait(long transportId, String storyId, String nodeId) {
        return new CanonicalSessionFrame(
            transportId,
            storyId,
            "session_a",
            nodeId,
            CanonicalSessionFrame.Kind.LINE,
            "Speaker",
            "Text",
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            "media/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png",
            null);
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
