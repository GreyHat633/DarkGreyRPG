package darkgrey.rpg.dialogue;

public final class ChoiceOption {

    private final String text;
    private final String next;

    public ChoiceOption(String text, String next) {
        this.text = text;
        this.next = next;
    }

    public String getText() {
        return text;
    }

    public String getNext() {
        return next;
    }
}
