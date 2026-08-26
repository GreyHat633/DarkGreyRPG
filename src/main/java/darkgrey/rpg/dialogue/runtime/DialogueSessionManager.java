package darkgrey.rpg.dialogue.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.dialogue.ChoiceNode;
import darkgrey.rpg.dialogue.ChoiceOption;
import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.DialogueNode;
import darkgrey.rpg.dialogue.LineNode;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.S2CDialogueClose;
import darkgrey.rpg.network.message.S2CDialogueFrame;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.runtime.ChatMessages;

public final class DialogueSessionManager {

    public static final int CONTINUE_ACTION = DialogueFlowEngine.CONTINUE_ACTION;

    private final ProjectRepository repository;
    private final AtomicLong nextSessionId = new AtomicLong(1L);
    private final Map<UUID, DialogueSession> sessions = new LinkedHashMap<UUID, DialogueSession>();
    private final Map<UUID, DialogueResult> lastResults = new LinkedHashMap<UUID, DialogueResult>();
    private final List<DialogueResultListener> resultListeners = new ArrayList<DialogueResultListener>();

    public DialogueSessionManager(ProjectRepository repository) {
        this.repository = repository;
    }

    public boolean start(EntityPlayerMP player, String dialogueId) {
        DialogueDefinition dialogue = repository.getSnapshot()
            .getDialogue(dialogueId);
        if (dialogue == null) {
            ChatMessages.error(player, "Unknown Dialogue ID: " + dialogueId);
            return false;
        }
        DialogueSession session = new DialogueSession(nextSessionId.getAndIncrement(), dialogue);
        sessions.put(player.getUniqueID(), session);
        sendCurrentFrame(player, session);
        return true;
    }

    public void handleAction(EntityPlayerMP player, long sessionId, String nodeId, int choiceIndex) {
        DialogueSession session = sessions.get(player.getUniqueID());
        if (session == null || session.getId() != sessionId
            || !session.getCurrentNodeId()
                .equals(nodeId)) {
            return;
        }

        DialogueNode node = session.getDialogue()
            .getNode(nodeId);
        String nextNodeId = DialogueFlowEngine.advance(node, choiceIndex);
        if (nextNodeId == null) {
            return;
        }
        session.setCurrentNodeId(nextNodeId);
        sendCurrentFrame(player, session);
    }

    public DialogueResult getLastResult(UUID playerId) {
        return lastResults.get(playerId);
    }

    public void clearSessions() {
        sessions.clear();
    }

    public void addResultListener(DialogueResultListener listener) {
        resultListeners.add(listener);
    }

    private void sendCurrentFrame(EntityPlayerMP player, DialogueSession session) {
        DialogueFlowEngine.ResolvedStep resolved = DialogueFlowEngine
            .resolve(session.getDialogue(), session.getCurrentNodeId());
        if (resolved.getError() != null) {
            fail(player, session, resolved.getError());
            return;
        }
        if (resolved.getResult() != null) {
            complete(player, session, resolved.getResult());
            return;
        }
        DialogueNode node = resolved.getNode();
        session.setCurrentNodeId(node.getId());
        if (node instanceof LineNode) {
            LineNode line = (LineNode) node;
            ActorDefinition actor = repository.getSnapshot()
                .getActor(line.getSpeaker());
            String speakerName = actor == null ? line.getSpeaker() : actor.getDisplayName();
            DialogueNetwork.CHANNEL.sendTo(
                new S2CDialogueFrame(
                    session.getId(),
                    session.getDialogue()
                        .getId(),
                    line.getId(),
                    speakerName,
                    line.getText(),
                    Collections.<String>emptyList(),
                    true),
                player);
            return;
        }
        if (node instanceof ChoiceNode) {
            ChoiceNode choice = (ChoiceNode) node;
            java.util.List<String> labels = new java.util.ArrayList<String>();
            for (ChoiceOption option : choice.getChoices()) {
                labels.add(option.getText());
            }
            DialogueNetwork.CHANNEL.sendTo(
                new S2CDialogueFrame(
                    session.getId(),
                    session.getDialogue()
                        .getId(),
                    choice.getId(),
                    "",
                    choice.getPrompt(),
                    labels,
                    false),
                player);
            return;
        }
    }

    private void complete(EntityPlayerMP player, DialogueSession session, String result) {
        sessions.remove(player.getUniqueID());
        DialogueResult dialogueResult = new DialogueResult(
            player.getUniqueID(),
            session.getDialogue()
                .getId(),
            result);
        lastResults.put(player.getUniqueID(), dialogueResult);
        DialogueNetwork.CHANNEL.sendTo(new S2CDialogueClose(session.getId(), result), player);
        ChatMessages.success(
            player,
            "Dialogue " + session.getDialogue()
                .getId() + " returned Result: " + result);
        for (DialogueResultListener listener : resultListeners) {
            listener.onDialogueResult(player, dialogueResult);
        }
    }

    private void fail(EntityPlayerMP player, DialogueSession session, String message) {
        sessions.remove(player.getUniqueID());
        DialogueNetwork.CHANNEL.sendTo(new S2CDialogueClose(session.getId(), "error"), player);
        ChatMessages.error(player, message);
    }
}
