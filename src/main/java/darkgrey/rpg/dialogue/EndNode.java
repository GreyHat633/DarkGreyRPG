package darkgrey.rpg.dialogue;

public final class EndNode extends DialogueNode {

    private final String result;

    public EndNode(String id, String result) {
        super(id, DialogueNodeType.END);
        this.result = result;
    }

    public String getResult() {
        return result;
    }
}
