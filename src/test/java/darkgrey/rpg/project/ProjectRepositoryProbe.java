package darkgrey.rpg.project;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Arrays;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.dialogue.EndNode;
import darkgrey.rpg.dialogue.runtime.DialogueFlowEngine;
import darkgrey.rpg.live.LiveBridgeServer;
import darkgrey.rpg.network.message.C2SDialogueAction;
import darkgrey.rpg.network.message.S2CDialogueClose;
import darkgrey.rpg.network.message.S2CDialogueFrame;
import darkgrey.rpg.network.message.S2CQuestJournal;
import darkgrey.rpg.quest.ObjectiveGroupMode;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.quest.runtime.QuestLedger;
import darkgrey.rpg.quest.runtime.QuestProgressEvaluator;
import darkgrey.rpg.quest.runtime.QuestProgressRecord;
import darkgrey.rpg.quest.runtime.QuestStatus;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryNodeType;
import darkgrey.rpg.story.runtime.BuiltinStoryExecutors;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryVariableLedger;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class ProjectRepositoryProbe {

    private ProjectRepositoryProbe() {}

    public static void main(String[] arguments) throws Exception {
        if (arguments.length < 1 || arguments.length > 2) {
            throw new IllegalArgumentException("Expected probe directory and optional Phase 4 example");
        }

        File root = new File(arguments[0]);
        File actors = new File(root, "actors");
        if (!actors.isDirectory() && !actors.mkdirs()) {
            throw new IllegalStateException("Cannot create " + actors);
        }
        File dialogues = new File(root, "dialogues");
        if (!dialogues.isDirectory() && !dialogues.mkdirs()) {
            throw new IllegalStateException("Cannot create " + dialogues);
        }
        File quests = new File(root, "quests");
        if (!quests.isDirectory() && !quests.mkdirs()) {
            throw new IllegalStateException("Cannot create " + quests);
        }
        File stories = new File(root, "stories");
        if (!stories.isDirectory() && !stories.mkdirs()) {
            throw new IllegalStateException("Cannot create " + stories);
        }

        write(
            new File(root, "project.json"),
            "{\n" + "  \"schema_version\": 2,\n"
                + "  \"id\": \"runtime_probe\",\n"
                + "  \"display_name\": \"Runtime Probe\"\n"
                + "}\n");
        write(
            new File(quests, "probe_quest.json"),
            "{\n" + "  \"schema_version\": 2,\n"
                + "  \"id\": \"probe_quest\",\n"
                + "  \"title\": \"Probe Quest\",\n"
                + "  \"display_name\": \"Probe Quest\",\n"
                + "  \"description\": \"Quest runtime probe\",\n"
                + "  \"home_story_id\": \"probe_story\",\n"
                + "  \"objectives\": [\n"
                + "    {\"id\":\"kill\",\"type\":\"kill_entity\",\"description\":\"Kill Slimes\","
                + "\"entity\":\"Slime\",\"required\":10},\n"
                + "    {\"id\":\"collect\",\"type\":\"collect_item\",\"description\":\"Collect Stone\","
                + "\"item\":\"minecraft:stone\",\"metadata\":-1,\"required\":2},\n"
                + "    {\"id\":\"reach\",\"type\":\"reach_location\",\"description\":\"Reach target\","
                + "\"dimension\":0,\"x\":1,\"y\":64,\"z\":1,\"radius\":3},\n"
                + "    {\"id\":\"interact\",\"type\":\"interact_actor\",\"description\":\"Meet actor\","
                + "\"actor_id\":\"probe_actor\",\"required\":1}\n"
                + "  ],\n"
                + "  \"objective_groups\": [\n"
                + "    {\"id\":\"combat\",\"mode\":\"ALL\",\"objectives\":[\"kill\"]},\n"
                + "    {\"id\":\"route\",\"mode\":\"SEQUENCE\",\"objectives\":[\"interact\",\"reach\"]},\n"
                + "    {\"id\":\"supplies\",\"mode\":\"ANY\",\"objectives\":[\"collect\"]}\n"
                + "  ],\n"
                + "  \"metadata\": {\"notes\":\"Probe\",\"tags\":[\"test\"]}\n"
                + "}\n");
        write(
            new File(stories, "probe_story.json"),
            "{\n" + "  \"schema_version\":2,\n"
                + "  \"id\":\"probe_story\",\n"
                + "  \"title\":\"Probe Story\",\n"
                + "  \"display_name\":\"Probe Story\",\n"
                + "  \"description\":\"Schema 2 compatibility probe\",\n"
                + "  \"tags\":[\"probe\"],\n"
                + "  \"entry_presentation\":{\"mode\":\"none\",\"eyebrow\":\"\",\"title\":\"\",\"duration_seconds\":4.0},\n"
                + "  \"owned_resources\":{\"actors\":[\"probe_actor\"],\"dialogues\":[\"probe_dialogue\"],\"quests\":[\"probe_quest\"]},\n"
                + "  \"referenced_resources\":{\"actors\":[],\"dialogues\":[],\"quests\":[]},\n"
                + "  \"flow_ref\":\"probe_story\",\n"
                + "  \"entry\":\"interact\",\n"
                + "  \"nodes\":[\n"
                + "    {\"id\":\"interact\",\"type\":\"interact_actor\",\"position\":{\"x\":0,\"y\":0},"
                + "\"properties\":{\"actor_id\":\"probe_actor\"}},\n"
                + "    {\"id\":\"state\",\"type\":\"quest_state\",\"position\":{\"x\":200,\"y\":0},"
                + "\"properties\":{\"quest_id\":\"probe_quest\",\"state\":\"NOT_STARTED\"}},\n"
                + "    {\"id\":\"dialogue\",\"type\":\"play_dialogue\",\"position\":{\"x\":400,\"y\":0},"
                + "\"properties\":{\"dialogue_id\":\"probe_dialogue\"}},\n"
                + "    {\"id\":\"message\",\"type\":\"send_message\",\"position\":{\"x\":400,\"y\":160},"
                + "\"properties\":{\"message\":\"Already started\"}},\n"
                + "    {\"id\":\"end\",\"type\":\"end\",\"position\":{\"x\":650,\"y\":80},\"properties\":{}}\n"
                + "  ],\n"
                + "  \"connections\":[\n"
                + "    {\"from\":\"interact\",\"output\":\"next\",\"to\":\"state\"},\n"
                + "    {\"from\":\"state\",\"output\":\"true\",\"to\":\"dialogue\"},\n"
                + "    {\"from\":\"state\",\"output\":\"false\",\"to\":\"message\"},\n"
                + "    {\"from\":\"dialogue\",\"output\":\"accept\",\"to\":\"end\"},\n"
                + "    {\"from\":\"message\",\"output\":\"next\",\"to\":\"end\"}\n"
                + "  ],\n"
                + "  \"metadata\":{\"notes\":\"Probe\",\"tags\":[\"test\"]}\n"
                + "}\n");
        File actorFile = new File(actors, "probe_actor.json");
        write(
            actorFile,
            "{\n" + "  \"schema_version\": 2,\n"
                + "  \"id\": \"probe_actor\",\n"
                + "  \"display_name\": \"Probe Actor\",\n"
                + "  \"notes\": \"Loader behavior probe\",\n"
                + "  \"tags\": [\"probe\"],\n"
                + "  \"home_story_id\": \"probe_story\"\n"
                + "}\n");
        write(
            new File(dialogues, "probe_dialogue.json"),
            "{\n" + "  \"schema_version\": 2,\n"
                + "  \"id\": \"probe_dialogue\",\n"
                + "  \"title\": \"Probe Dialogue\",\n"
                + "  \"display_name\": \"Probe Dialogue\",\n"
                + "  \"home_story_id\": \"probe_story\",\n"
                + "  \"speakers\": [\"probe_actor\"],\n"
                + "  \"entry\": \"hello\",\n"
                + "  \"nodes\": [\n"
                + "    {\"id\":\"hello\",\"type\":\"line\",\"speaker\":\"probe_actor\","
                + "\"text\":\"Hello\",\"next\":\"choose\"},\n"
                + "    {\"id\":\"choose\",\"type\":\"choice\",\"prompt\":\"Choose\","
                + "\"choices\":[{\"text\":\"Accept\",\"next\":\"end\"},{\"text\":\"More\",\"next\":\"back\"}]},\n"
                + "    {\"id\":\"back\",\"type\":\"jump\",\"target\":\"choose\"},\n"
                + "    {\"id\":\"end\",\"type\":\"end\",\"result\":\"accept\"}\n"
                + "  ],\n"
                + "  \"metadata\": {\"notes\":\"Probe\",\"tags\":[\"test\"]}\n"
                + "}\n");

        ProjectRepository repository = new ProjectRepository(root);
        ProjectRepository.ReloadResult firstReload = repository.reload();
        require(firstReload.isSuccessful(), firstReload.getSummary());
        require(
            repository.getSnapshot()
                .getActors()
                .size() == 1,
            "Expected one Actor");
        require(
            "Probe Actor".equals(
                repository.getSnapshot()
                    .getActor("probe_actor")
                    .getDisplayName()),
            "Actor fields were not loaded");
        require(
            repository.getSnapshot()
                .getDialogues()
                .size() == 1,
            "Expected one Dialogue");
        require(
            repository.getSnapshot()
                .getDialogue("probe_dialogue")
                .getNode("end") instanceof EndNode,
            "Dialogue nodes were not loaded");
        assertDialogueFlow(
            repository.getSnapshot()
                .getDialogue("probe_dialogue"));
        assertQuestFlow(
            repository.getSnapshot()
                .getQuest("probe_quest"));
        assertStoryFlow(
            repository.getSnapshot()
                .getStory("probe_story"));
        assertNetworkCodecs();
        if (arguments.length == 2) {
            assertPhase4Example(new File(arguments[1]));
        }

        write(
            actorFile,
            "{\n" + "  \"schema_version\": 1,\n"
                + "  \"id\": \"probe_actor\",\n"
                + "  \"display_name\": \"Invalid Actor\",\n"
                + "  \"dialogue\": \"forbidden\"\n"
                + "}\n");

        ProjectRepository.ReloadResult rejectedReload = repository.reload();
        require(!rejectedReload.isSuccessful(), "Out-of-scope Actor field was accepted");
        require(
            "Probe Actor".equals(
                repository.getSnapshot()
                    .getActor("probe_actor")
                    .getDisplayName()),
            "Failed reload replaced the last valid snapshot");

        System.out.println("RUNTIME_PROJECT_LOAD_PROBE=PASS");
        System.out.println("RUNTIME_ACTOR_SCOPE_GUARD=PASS");
        System.out.println("RUNTIME_FAILED_RELOAD_ROLLBACK=PASS");
        System.out.println("RUNTIME_DIALOGUE_LOAD_PROBE=PASS");
        System.out.println("RUNTIME_DIALOGUE_RESULT_PROBE=PASS");
        System.out.println("DIALOGUE_NETWORK_CODEC_PROBE=PASS");
        System.out.println("RUNTIME_QUEST_LOAD_PROBE=PASS");
        System.out.println("RUNTIME_QUEST_GROUP_PROBE=PASS");
        System.out.println("PLAYER_QUEST_NBT_PROBE=PASS");
        System.out.println("PLAYER_QUEST_ISOLATION_PROBE=PASS");
        System.out.println("QUEST_JOURNAL_CODEC_PROBE=PASS");
        System.out.println("RUNTIME_STORY_LOAD_PROBE=PASS");
        System.out.println("RUNTIME_SCHEMA2_EDITOR_FIELDS_PROBE=PASS");
        System.out.println("RUNTIME_DIALOGUE_QUEST_SCHEMA2_EDITOR_FIELDS_PROBE=PASS");
        System.out.println("STORY_EXECUTOR_REGISTRY_PROBE=PASS");
        System.out.println("STORY_VARIABLE_NBT_PROBE=PASS");
        System.out.println("PLAY_TEST_SNAPSHOT_PROBE=PASS");
        System.out.println("STORY_DEBUG_TRACE_PROBE=PASS");
        require(LiveBridgeServer.PROTOCOL_VERSION == 1, "Unexpected Live Bridge protocol version");
        System.out.println("LIVE_JSON_LINES_PROTOCOL_PROBE=PASS");
        if (arguments.length == 2) {
            System.out.println("PHASE4_ACCEPTANCE_GRAPH_PROBE=PASS");
        }
    }

    private static void write(File file, String content) throws Exception {
        Files.write(file.toPath(), content.getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean condition, String message) {
        if (!condition) {
            throw new AssertionError(message);
        }
    }

    private static void assertDialogueFlow(darkgrey.rpg.dialogue.DialogueDefinition dialogue) {
        DialogueFlowEngine.ResolvedStep line = DialogueFlowEngine.resolve(dialogue, dialogue.getEntry());
        require(
            "hello".equals(
                line.getNode()
                    .getId()),
            "Dialogue entry did not resolve to Line");
        String choiceId = DialogueFlowEngine.advance(line.getNode(), DialogueFlowEngine.CONTINUE_ACTION);
        DialogueFlowEngine.ResolvedStep choice = DialogueFlowEngine.resolve(dialogue, choiceId);
        require(
            "choose".equals(
                choice.getNode()
                    .getId()),
            "Line did not advance to Choice");
        String endId = DialogueFlowEngine.advance(choice.getNode(), 0);
        DialogueFlowEngine.ResolvedStep end = DialogueFlowEngine.resolve(dialogue, endId);
        require("accept".equals(end.getResult()), "Choice returned the wrong Result");
        String jumpId = DialogueFlowEngine.advance(choice.getNode(), 1);
        DialogueFlowEngine.ResolvedStep jumped = DialogueFlowEngine.resolve(dialogue, jumpId);
        require(
            "choose".equals(
                jumped.getNode()
                    .getId()),
            "Jump did not return to Choice");
    }

    private static void assertNetworkCodecs() {
        ByteBuf frameBuffer = Unpooled.buffer();
        new S2CDialogueFrame(42L, "probe_dialogue", "choose", "酒馆老板", "请选择。", Arrays.asList("接受", "拒绝"), false)
            .toBytes(frameBuffer);
        S2CDialogueFrame decodedFrame = new S2CDialogueFrame();
        decodedFrame.fromBytes(frameBuffer);
        require(decodedFrame.getSessionId() == 42L, "Frame session ID changed");
        require(
            decodedFrame.getChoices()
                .size() == 2,
            "Frame choices changed");
        require("酒馆老板".equals(decodedFrame.getSpeakerName()), "Frame UTF-8 changed");

        ByteBuf actionBuffer = Unpooled.buffer();
        new C2SDialogueAction(42L, "choose", 1).toBytes(actionBuffer);
        C2SDialogueAction decodedAction = new C2SDialogueAction();
        decodedAction.fromBytes(actionBuffer);
        require(decodedAction.getSessionId() == 42L, "Action session ID changed");
        require(decodedAction.getChoiceIndex() == 1, "Action choice changed");

        ByteBuf closeBuffer = Unpooled.buffer();
        new S2CDialogueClose(42L, "accept").toBytes(closeBuffer);
        S2CDialogueClose decodedClose = new S2CDialogueClose();
        decodedClose.fromBytes(closeBuffer);
        require("accept".equals(decodedClose.getResult()), "Close Result changed");

        ByteBuf journalBuffer = Unpooled.buffer();
        QuestJournalEntry journalEntry = new QuestJournalEntry(
            "probe_quest",
            "探针任务",
            "独立任务描述",
            QuestStatus.ACTIVE,
            Arrays.asList("消灭史莱姆 4/10"));
        new S2CQuestJournal(Arrays.asList(journalEntry)).toBytes(journalBuffer);
        S2CQuestJournal decodedJournal = new S2CQuestJournal();
        decodedJournal.fromBytes(journalBuffer);
        require(
            decodedJournal.getEntries()
                .size() == 1,
            "Quest Journal entry count changed");
        require(
            "消灭史莱姆 4/10".equals(
                decodedJournal.getEntries()
                    .get(0)
                    .getObjectiveLines()
                    .get(0)),
            "Quest Journal UTF-8 changed");
    }

    private static void assertQuestFlow(QuestDefinition quest) {
        require(
            quest != null && quest.getObjectives()
                .size() == 4,
            "Quest objectives were not loaded");
        require(
            quest.getObjectiveGroups()
                .get(1)
                .getMode() == ObjectiveGroupMode.SEQUENCE,
            "SEQUENCE group was not loaded");

        QuestProgressRecord record = new QuestProgressRecord(quest.getId());
        for (int index = 0; index < 9; index++) {
            require(QuestProgressEvaluator.addProgress(quest, record, "kill", 1), "Kill progress was rejected");
        }
        require(record.getProgress("kill") == 9, "Kill progress did not reach 9/10");
        require(!QuestProgressEvaluator.canProgress(quest, record, "reach"), "SEQUENCE allowed second objective early");
        require(QuestProgressEvaluator.addProgress(quest, record, "interact", 1), "First SEQUENCE objective failed");
        require(QuestProgressEvaluator.addProgress(quest, record, "reach", 1), "Second SEQUENCE objective failed");
        require(QuestProgressEvaluator.addProgress(quest, record, "collect", 2), "ANY objective failed");
        require(QuestProgressEvaluator.addProgress(quest, record, "kill", 1), "Tenth Kill progress failed");
        require(record.getStatus() == QuestStatus.COMPLETED, "Quest did not complete at 10/10");

        NBTTagCompound saved = record.writeToNbt();
        QuestProgressRecord loaded = QuestProgressRecord.readFromNbt(saved);
        require(loaded.getStatus() == QuestStatus.COMPLETED, "Quest status was not persisted");
        require(loaded.getProgress("kill") == 10, "Quest progress was not persisted");

        QuestLedger ledger = new QuestLedger();
        ledger.startQuest("player-one", quest.getId())
            .setProgress("kill", 7);
        ledger.startQuest("player-two", quest.getId())
            .setProgress("kill", 2);
        NBTTagCompound ledgerTag = new NBTTagCompound();
        ledger.writeToNbt(ledgerTag);
        QuestLedger restoredLedger = new QuestLedger();
        restoredLedger.readFromNbt(ledgerTag);
        require(
            restoredLedger.getQuest("player-one", quest.getId())
                .getProgress("kill") == 7,
            "First player's Quest progress was not isolated");
        require(
            restoredLedger.getQuest("player-two", quest.getId())
                .getProgress("kill") == 2,
            "Second player's Quest progress was not isolated");
        NBTTagCompound playerSnapshot = restoredLedger.snapshotPlayer("player-one");
        restoredLedger.getQuest("player-one", quest.getId())
            .setProgress("kill", 1);
        restoredLedger.restorePlayer("player-one", playerSnapshot);
        require(
            restoredLedger.getQuest("player-one", quest.getId())
                .getProgress("kill") == 7,
            "Play Test Quest snapshot did not restore");
    }

    private static void assertStoryFlow(StoryDefinition story) {
        require(
            story != null && story.getNodes()
                .size() == 5,
            "Story nodes were not loaded");
        require(
            "state".equals(
                story.findConnection("interact", "next")
                    .getTo()),
            "Story connection lookup failed");
        for (StoryNodeType type : StoryNodeType.values()) {
            require(
                BuiltinStoryExecutors.createRegistry()
                    .get(type) != null,
                "Missing Story executor for " + type);
        }
        StoryVariableLedger ledger = new StoryVariableLedger();
        ledger.set("player-one", story.getId(), "reward_claimed", "true");
        ledger.set("player-two", story.getId(), "reward_claimed", "false");
        NBTTagCompound saved = new NBTTagCompound();
        ledger.writeToNbt(saved);
        StoryVariableLedger loaded = new StoryVariableLedger();
        loaded.readFromNbt(saved);
        require(
            "true".equals(loaded.get("player-one", story.getId(), "reward_claimed")),
            "Story variable was not persisted");
        require(
            "false".equals(loaded.get("player-two", story.getId(), "reward_claimed")),
            "Story variables were not player-isolated");
        NBTTagCompound playerSnapshot = loaded.snapshotPlayer("player-one");
        loaded.set("player-one", story.getId(), "reward_claimed", "changed");
        loaded.restorePlayer("player-one", playerSnapshot);
        require(
            "true".equals(loaded.get("player-one", story.getId(), "reward_claimed")),
            "Play Test Story-variable snapshot did not restore");
        StoryInstance instance = new StoryInstance(story);
        instance.recordDebug("interact", "WAIT", "Actor expected='probe_actor', actual='MANUAL', matches=false");
        StoryInstance.Snapshot instanceSnapshot = instance.snapshot();
        instance.startAt("end_false");
        instance.restore(instanceSnapshot);
        require("interact".equals(instance.getLastNodeId()), "Story debug trace did not restore");
        require(
            instance.getLastExplanation()
                .contains("matches=false"),
            "Why Not Triggered explanation did not restore");
    }

    private static void assertPhase4Example(File exampleDirectory) {
        ProjectRepository repository = new ProjectRepository(exampleDirectory);
        ProjectRepository.ReloadResult result = repository.reload();
        require(result.isSuccessful(), result.getSummary());
        require(result.getStoryCount() == 1, "Expected one Phase 4 Story");
        StoryDefinition story = repository.getSnapshot()
            .getStory("tavern_slime_request");
        require(
            story != null && story.getNodes()
                .size() == 20,
            "Acceptance Story graph was not loaded");
        require(
            "not_started".equals(
                story.findConnection("interact_owner", "next")
                    .getTo()),
            "Actor interaction route changed");
        require(
            "start_quest".equals(
                story.findConnection("offer_dialogue", "accept")
                    .getTo()),
            "Dialogue accept route changed");
        require(
            "wait_completion".equals(
                story.findConnection("started_message", "next")
                    .getTo())
                && "interact_return".equals(
                    story.findConnection("wait_completion", "next")
                        .getTo()),
            "Quest completion and return interaction route changed");
        require(
            "complete_dialogue".equals(
                story.findConnection("reward_claimed", "false")
                    .getTo()),
            "Reward route changed");
        require(
            "give_xp".equals(
                story.findConnection("complete_dialogue", "done")
                    .getTo()),
            "Completion Dialogue route changed");
    }
}
