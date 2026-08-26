package darkgrey.rpg.quest.runtime;

import darkgrey.rpg.quest.ObjectiveType;

public final class QuestEvent {

    private final ObjectiveType type;
    private final String targetId;
    private final int metadata;
    private final int dimension;
    private final double x;
    private final double y;
    private final double z;
    private final int amount;

    private QuestEvent(ObjectiveType type, String targetId, int metadata, int dimension, double x, double y, double z,
        int amount) {
        this.type = type;
        this.targetId = targetId;
        this.metadata = metadata;
        this.dimension = dimension;
        this.x = x;
        this.y = y;
        this.z = z;
        this.amount = amount;
    }

    public static QuestEvent target(ObjectiveType type, String targetId, int metadata, int amount) {
        return new QuestEvent(type, targetId, metadata, 0, 0.0D, 0.0D, 0.0D, amount);
    }

    public static QuestEvent location(int dimension, double x, double y, double z) {
        return new QuestEvent(ObjectiveType.REACH_LOCATION, "", -1, dimension, x, y, z, 1);
    }

    public ObjectiveType getType() {
        return type;
    }

    public String getTargetId() {
        return targetId;
    }

    public int getMetadata() {
        return metadata;
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

    public int getAmount() {
        return amount;
    }
}
