package darkgrey.rpg.dialogue;

public final class JumpNode extends DialogueNode {

    private final String target;

    public JumpNode(String id, String target) {
        super(id, DialogueNodeType.JUMP);
        this.target = target;
    }

    public String getTarget() {
        return target;
    }
}
