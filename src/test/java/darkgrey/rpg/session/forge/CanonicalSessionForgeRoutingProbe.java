package darkgrey.rpg.session.forge;

import java.io.File;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;

/** Verifies canonical Frame/Close routing without constructing a live Minecraft player or process. */
public final class CanonicalSessionForgeRoutingProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");

    private CanonicalSessionForgeRoutingProbe() {}

    public static void main(String[] args) {
        RecordingSender sender = new RecordingSender();
        CanonicalSessionFrame frame = new CanonicalSessionFrame(
            1L,
            "story",
            "session",
            "line",
            CanonicalSessionFrame.Kind.LINE,
            "Speaker",
            "Text",
            Collections.emptyList());
        CanonicalSessionForgeManager.route(sender, null, CanonicalSessionDispatch.frame(frame));
        require(sender.frames == 1 && sender.closes == 0, "Frame routes only to sendFrame");

        CanonicalSessionClose close = new CanonicalSessionClose(1L, "story");
        CanonicalSessionCompletionResult result = new CanonicalSessionCompletionResult(
            PLAYER,
            "story",
            "placement",
            "session",
            1L,
            "end",
            Collections.<String, Boolean>emptyMap());
        CanonicalSessionForgeManager.route(sender, null, CanonicalSessionDispatch.completed(close, result));
        require(sender.frames == 1 && sender.closes == 1, "Close routes only to sendClose");

        ProjectSnapshot project = CanonicalSessionForgeProbeProject.create();
        CanonicalSessionForgeManager manager = new CanonicalSessionForgeManager(
            new ProjectRepository(new File(".")),
            new CanonicalSessionForgeManager.SavedDataProvider() {

                @Override
                public CanonicalSessionSavedData get(net.minecraft.entity.player.EntityPlayerMP player,
                    darkgrey.rpg.session.instance.CanonicalSessionResourceResolver resolver) {
                    return null;
                }
            },
            sender);
        CanonicalSessionSavedData activeData = new CanonicalSessionSavedData();
        require(
            manager.startTrustedForProbe(PLAYER, null, project, activeData, "story_a", "place_a"),
            "Session start seam succeeds");
        require(sender.frames == 2 && sender.closes == 1, "Session start routes one Frame only");

        CanonicalSessionSavedData completedData = new CanonicalSessionSavedData();
        CanonicalSessionServerService service = new CanonicalSessionServerService(project, completedData);
        CanonicalSessionDispatch first = service.start(PLAYER, "story_a", "place_a");
        service.continueLine(
            PLAYER,
            "story_a",
            first.getFrame()
                .getTransportId(),
            first.getFrame()
                .getCurrentNodeId());
        sender.events.clear();
        manager.bindStoryContinuationListener(new CanonicalSessionForgeManager.StoryContinuationListener() {

            @Override
            public void onStoryContinuation(net.minecraft.entity.player.EntityPlayerMP player, String storyId) {
                sender.events.add("continue");
            }
        });
        require(
            manager.resumeTrustedForProbe(PLAYER, null, project, completedData, "story_a"),
            "Session resume seam succeeds");
        require(sender.frames == 2 && sender.closes == 2, "Session resume routes one Close only");
        require(
            sender.events.equals(Arrays.asList("close", "continue")),
            "Old Session Close must precede Story continuation");
        require(completedData.getSnapshot(PLAYER, "story_a") == null, "Resume consumes completion");
        require(completedData.getPendingContinuation(PLAYER, "story_a") != null, "Resume stores continuation");
        int framesBeforeFailure = sender.frames;
        int closesBeforeFailure = sender.closes;
        CanonicalSessionSavedData failingData = new CanonicalSessionSavedData();
        CanonicalSessionServerService failingService = new CanonicalSessionServerService(project, failingData);
        CanonicalSessionDispatch failingStart = failingService.start(PLAYER, "story_a", "place_a");
        failingService.continueLine(
            PLAYER,
            "story_a",
            failingStart.getFrame()
                .getTransportId(),
            failingStart.getFrame()
                .getCurrentNodeId());
        require(
            !manager.resumeTrustedForProbe(
                PLAYER,
                null,
                CanonicalSessionForgeProbeProject.create(false),
                failingData,
                "story_a"),
            "invalid completion route rejects");
        require(
            sender.frames == framesBeforeFailure && sender.closes == closesBeforeFailure,
            "failed route sends no envelope");
        require(failingData.getSnapshot(PLAYER, "story_a") != null, "failed route retains completed Session");

        require(!manager.start(null, "story", "placement"), "Missing player rejects Session start");
        require(!manager.resume(null, "story"), "Missing player rejects Session resume");
        require(!manager.handleAction(null, null), "Missing player and action are rejected");
        System.out.println("CANONICAL_SESSION_FORGE_ROUTING_PROBE=PASS");
        System.out.println("CANONICAL_SESSION_FORGE_IDENTITY_FAILURES=PASS");
    }

    private static final class RecordingSender implements CanonicalSessionForgeManager.Sender {

        private int frames;
        private int closes;
        private final List<String> events = new ArrayList<String>();

        @Override
        public void sendFrame(net.minecraft.entity.player.EntityPlayerMP player, CanonicalSessionFrame frame) {
            frames++;
        }

        @Override
        public void sendClose(net.minecraft.entity.player.EntityPlayerMP player, CanonicalSessionClose close) {
            closes++;
            events.add("close");
        }
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
