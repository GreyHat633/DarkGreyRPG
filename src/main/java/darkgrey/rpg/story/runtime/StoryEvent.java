package darkgrey.rpg.story.runtime;

public final class StoryEvent {

    public enum Type {
        INTERACT_ACTOR,
        PLAYER_POSITION,
        QUEST_COMPLETED,
        DIALOGUE_RESULT,
        MANUAL
    }

    private final Type type;
    private final String targetId;
    private final String result;
    private final int dimension;
    private final double x;
    private final double y;
    private final double z;

    private StoryEvent(Type type, String targetId, String result, int dimension, double x, double y, double z) {
        this.type = type;
        this.targetId = targetId;
        this.result = result;
        this.dimension = dimension;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static StoryEvent target(Type type, String targetId) {
        return new StoryEvent(type, targetId, "", 0, 0.0D, 0.0D, 0.0D);
    }

    public static StoryEvent dialogueResult(String dialogueId, String result) {
        return new StoryEvent(Type.DIALOGUE_RESULT, dialogueId, result, 0, 0.0D, 0.0D, 0.0D);
    }

    public static StoryEvent position(int dimension, double x, double y, double z) {
        return new StoryEvent(Type.PLAYER_POSITION, "", "", dimension, x, y, z);
    }

    public static StoryEvent manual() {
        return new StoryEvent(Type.MANUAL, "", "", 0, 0.0D, 0.0D, 0.0D);
    }

    public Type getType() {
        return type;
    }

    public String getTargetId() {
        return targetId;
    }

    public String getResult() {
        return result;
    }

    public int getDimension() {
        return dimension;
    }

    public double getX() {
        return x;
    }

    public double getY() {
        return y;
    }

    public double getZ() {
        return z;
    }
}
