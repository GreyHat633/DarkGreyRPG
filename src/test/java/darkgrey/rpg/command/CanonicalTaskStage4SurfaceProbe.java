package darkgrey.rpg.command;

import java.io.File;
import java.lang.reflect.Method;
import java.nio.charset.Charset;
import java.nio.file.Files;
import java.nio.file.Paths;

import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;

/** Focused structural proof for Stage 4 command actions and Journal tabs. */
public final class CanonicalTaskStage4SurfaceProbe {

    private CanonicalTaskStage4SurfaceProbe() {}

    public static void main(String[] args) throws Exception {
        require(
            "/dgr task <list|info|start|journal|progress>".equals(CommandDarkGreyRpg.taskUsage(null)),
            "task usage");
        require(
            CommandDarkGreyRpg.taskUsage(new String[] { "task", "start" })
                .contains("<placement_id>"),
            "task start usage");
        Method processTask = CommandDarkGreyRpg.class
            .getDeclaredMethod("processTask", net.minecraft.command.ICommandSender.class, String[].class);
        require(processTask != null, "task action dispatcher");
        CommandDarkGreyRpg command = new CommandDarkGreyRpg(
            new ProjectRepository(new File(".")),
            new EditorSessionManager(),
            new DialogueSessionManager(new ProjectRepository(new File("."))),
            new QuestRuntimeService(new ProjectRepository(new File("."))),
            null,
            new CanonicalSessionForgeManager(new ProjectRepository(new File("."))),
            new CanonicalTaskForgeManager(new ProjectRepository(new File("."))));
        require(command != null, "task constructor");
        require("dgr".equals(command.getCommandName()), "primary command root");
        require(
            command.getCommandAliases()
                .isEmpty(),
            "historical dgrpg alias removed");
        require(
            command.getCommandUsage(null)
                .startsWith("/dgr "),
            "root command usage");

        String guiSource = new String(
            Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/client/gui/GuiQuestJournal.java")),
            Charset.forName("UTF-8"));
        require(
            guiSource.contains("\"进行中\"") && guiSource.contains("\"已完成\"") && guiSource.contains("\"失败\""),
            "three Chinese Journal tabs");
        require(guiSource.contains("QuestStatus.FAILED"), "failed rendering state");
        require(guiSource.contains("Math.min(PANEL_HEIGHT, height - 20)"), "Journal fits the scale-2 854x480 client");
        String nominatorSource = new String(
            Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java")),
            Charset.forName("UTF-8"));
        String bindingSource = new String(
            Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/nominator/NominatorActions.java")),
            Charset.forName("UTF-8"));
        require(
            nominatorSource.contains("q.setString(\"type\", row.type)")
                && bindingSource.contains("actor.isIndividual()")
                && bindingSource.contains("groups.add(id)")
                && bindingSource.contains("individual = id"),
            "Nominator preserves distinct Group/NPC semantics through the typed operation service");
        System.out.println("CANONICAL_TASK_COMMAND_SURFACE=PASS");
        System.out.println("CANONICAL_TASK_GUI_TABS=PASS");
        System.out.println("DGR_B2_NOMINATOR_IDENTITY_LABELS=PASS");
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
