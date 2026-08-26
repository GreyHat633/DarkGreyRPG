package darkgrey.rpg.quest;

public final class ReachLocationObjective extends QuestObjective {

    private final int dimension;
    private final double x;
    private final double y;
    private final double z;
    private final double radius;

    public ReachLocationObjective(String id, String description, int dimension, double x, double y, double z,
        double radius) {
        super(id, ObjectiveType.REACH_LOCATION, description, 1);
        this.dimension = dimension;
        this.x = x;
        this.y = y;
        this.z = z;
        this.radius = radius;
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

    public double getRadius() {
        return radius;
    }
}
