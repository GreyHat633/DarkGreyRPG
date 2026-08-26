package darkgrey.rpg.dialogue;

public abstract class DialogueNode {

    private final String id;
    private final DialogueNodeType type;

    protected DialogueNode(String id, DialogueNodeType type) {
        this.id = id;
        this.type = type;
    }

    public String getId() {
        return id;
    }

    public DialogueNodeType getType() {
        return type;
    }
}
