package darkgrey.rpg.story.runtime;

public final class NodeExecutionResult {

    public enum Kind {
        NEXT,
        WAIT,
        END,
        ENTER_STORY,
        SEQUENCE,
        ERROR
    }

    private final Kind kind;
    private final String output;

    private NodeExecutionResult(Kind kind, String output) {
        this.kind = kind;
        this.output = output;
    }

    public static NodeExecutionResult next(String output) {
        return new NodeExecutionResult(Kind.NEXT, output);
    }

    public static NodeExecutionResult waitForEvent() {
        return new NodeExecutionResult(Kind.WAIT, "");
    }

    public static NodeExecutionResult end() {
        return new NodeExecutionResult(Kind.END, "");
    }

    public static NodeExecutionResult enterStory(String storyId) {
        return new NodeExecutionResult(Kind.ENTER_STORY, storyId);
    }

    public static NodeExecutionResult sequence() {
        return new NodeExecutionResult(Kind.SEQUENCE, "");
    }

    public static NodeExecutionResult error(String message) {
        return new NodeExecutionResult(Kind.ERROR, message);
    }

    public Kind getKind() {
        return kind;
    }

    public String getOutput() {
        return output;
    }
}
