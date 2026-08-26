package darkgrey.rpg.dialogue;

public final class LineNode extends DialogueNode {

    private final String speaker;
    private final String text;
    private final String next;

    public LineNode(String id, String speaker, String text, String next) {
        super(id, DialogueNodeType.LINE);
        this.speaker = speaker;
        this.text = text;
        this.next = next;
    }

    public String getSpeaker() {
        return speaker;
    }

    public String getText() {
        return text;
    }

    public String getNext() {
        return next;
    }
}
