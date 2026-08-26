package darkgrey.rpg.dialogue.runtime;

import darkgrey.rpg.dialogue.DialogueDefinition;

final class DialogueSession {

    private final long id;
    private final DialogueDefinition dialogue;
    private String currentNodeId;

    DialogueSession(long id, DialogueDefinition dialogue) {
        this.id = id;
        this.dialogue = dialogue;
        this.currentNodeId = dialogue.getEntry();
    }

    long getId() {
        return id;
    }

    DialogueDefinition getDialogue() {
        return dialogue;
    }

    String getCurrentNodeId() {
        return currentNodeId;
    }

    void setCurrentNodeId(String currentNodeId) {
        this.currentNodeId = currentNodeId;
    }
}
