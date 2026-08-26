package darkgrey.rpg.dialogue.runtime;

import java.util.UUID;

public final class DialogueResult {

    private final UUID playerId;
    private final String dialogueId;
    private final String result;

    public DialogueResult(UUID playerId, String dialogueId, String result) {
        this.playerId = playerId;
        this.dialogueId = dialogueId;
        this.result = result;
    }

    public UUID getPlayerId() {
        return playerId;
    }

    public String getDialogueId() {
        return dialogueId;
    }

    public String getResult() {
        return result;
    }
}
