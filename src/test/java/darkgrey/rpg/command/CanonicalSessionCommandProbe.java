package darkgrey.rpg.command;

import java.util.Arrays;
import java.util.Collections;

import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.forge.CanonicalSessionForgeProbeProject;

/** Verifies canonical Session command parsing and completion without a live Minecraft player. */
public final class CanonicalSessionCommandProbe {

    private CanonicalSessionCommandProbe() {}

    public static void main(String[] args) {
        require("/dgrpg session <play|resume>".equals(CommandDarkGreyRpg.sessionUsage(null)), "base Session usage");
        require(
            "/dgrpg session play <story_id> <aggregate_node_id>"
                .equals(CommandDarkGreyRpg.sessionUsage(new String[] { "session", "play" })),
            "play usage");
        require(
            "/dgrpg session resume <story_id>"
                .equals(CommandDarkGreyRpg.sessionUsage(new String[] { "session", "resume" })),
            "resume usage");
        require(
            "/dgrpg session <play|resume>".equals(CommandDarkGreyRpg.sessionUsage(new String[] { "session", "other" })),
            "unknown action usage");

        ProjectSnapshot project = CanonicalSessionForgeProbeProject.create();
        require(
            Arrays.asList("story_a")
                .equals(CommandDarkGreyRpg.canonicalSessionStoryIds(project)),
            "canonical Story completion");
        require(
            Arrays.asList("place_a")
                .equals(CommandDarkGreyRpg.canonicalSessionPlacementIds(project, "story_a")),
            "Session aggregate completion");
        require(
            Collections.emptyList()
                .equals(CommandDarkGreyRpg.canonicalSessionPlacementIds(project, "missing")),
            "missing Story completion");
        require(
            Collections.emptyList()
                .equals(CommandDarkGreyRpg.canonicalSessionStoryIds(null)),
            "missing snapshot completion");
        System.out.println("CANONICAL_SESSION_COMMAND_PROBE=PASS");
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
