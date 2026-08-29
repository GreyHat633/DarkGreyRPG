package darkgrey.rpg.graph.canonical;

/** Immutable detached canonical graph port. */
public final class CanonicalGraphPort {

    private final String id;
    private final String displayName;
    private final CanonicalGraphPortDirection direction;
    private final CanonicalGraphInterfaceKind kind;
    private final int order;

    public CanonicalGraphPort(String id, String displayName, CanonicalGraphPortDirection direction,
        CanonicalGraphInterfaceKind kind, int order) {
        this.id = id;
        this.displayName = displayName;
        this.direction = direction;
        this.kind = kind;
        this.order = order;
    }

    public String getId() {
        return id;
    }

    public String getPortId() {
        return id;
    }

    public String getDisplayName() {
        return displayName;
    }

    public CanonicalGraphPortDirection getDirection() {
        return direction;
    }

    public boolean isInput() {
        return direction == CanonicalGraphPortDirection.INPUT;
    }

    public boolean isOutput() {
        return !isInput();
    }

    public CanonicalGraphInterfaceKind getKind() {
        return kind;
    }

    public CanonicalGraphInterfaceKind getInterfaceKind() {
        return kind;
    }

    public int getOrder() {
        return order;
    }
}
