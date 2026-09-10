package darkgrey.rpg.command;

import java.nio.charset.Charset;
import java.nio.file.Files;
import java.nio.file.Paths;

/** Focused structural proof for the canonical Story/Task command boundary. */
public final class CanonicalStoryCommandProbe {

    private CanonicalStoryCommandProbe() {}

    public static void main(String[] args) throws Exception {
        String source = new String(
            Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java")),
            Charset.forName("UTF-8"));
        String journalSource = new String(
            Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/task/journal/CanonicalJournalService.java")),
            Charset.forName("UTF-8"));
        require(!source.contains("questRuntime."), "legacy Quest gameplay removed");
        require(!source.contains("storyRuntime."), "legacy Story gameplay removed");
        require(source.contains("canonicalStoryManager.snapshot(player, id)"), "canonical Story state source");
        require(source.contains("canonicalStoryManager.reset(player, arguments[2])"), "canonical Story reset source");
        require(source.contains("canonicalJournalService.getJournal(player)"), "canonical Journal progress source");
        require(
            journalSource.contains("CanonicalTaskLegacyJournalAdapter.adapt"),
            "canonical Journal adapter boundary");
        require(
            "/dgr task <list|info|start|journal|progress>".equals(CommandDarkGreyRpg.taskUsage(null)),
            "canonical Task usage");
        require(
            CommandDarkGreyRpg.sessionUsage(null)
                .startsWith("/dgr session"),
            "canonical Session usage");
        System.out.println("CANONICAL_STORY_COMMAND_JOURNAL_PROBE=PASS");
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
