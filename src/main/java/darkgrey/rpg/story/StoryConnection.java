package darkgrey.rpg.story;

public final class StoryConnection {

    private final String from;
    private final String output;
    private final String to;

    public StoryConnection(String from, String output, String to) {
        this.from = from;
        this.output = output;
        this.to = to;
    }

    public String getFrom() {
        return from;
    }

    public String getOutput() {
        return output;
    }

    public String getTo() {
        return to;
    }
}
