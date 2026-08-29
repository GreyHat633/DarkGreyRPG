package darkgrey.rpg.graph.canonical;

/** Immutable canonical graph edge. */
public final class CanonicalGraphConnection {

    private final String fromNodeId;
    private final String fromPortId;
    private final String toNodeId;
    private final String toPortId;
    private final CanonicalGraphInterfaceKind interfaceKind;

    public CanonicalGraphConnection(String fromNodeId, String fromPortId, String toNodeId, String toPortId,
        CanonicalGraphInterfaceKind interfaceKind) {
        this.fromNodeId = fromNodeId;
        this.fromPortId = fromPortId;
        this.toNodeId = toNodeId;
        this.toPortId = toPortId;
        this.interfaceKind = interfaceKind;
    }

    public String getFromNodeId() {
        return fromNodeId;
    }

    public String getFromPortId() {
        return fromPortId;
    }

    public String getToNodeId() {
        return toNodeId;
    }

    public String getToPortId() {
        return toPortId;
    }

    public CanonicalGraphInterfaceKind getInterfaceKind() {
        return interfaceKind;
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof CanonicalGraphConnection)) return false;
        CanonicalGraphConnection that = (CanonicalGraphConnection) other;
        return fromNodeId.equals(that.fromNodeId) && fromPortId.equals(that.fromPortId)
            && toNodeId.equals(that.toNodeId)
            && toPortId.equals(that.toPortId)
            && interfaceKind == that.interfaceKind;
    }

    @Override
    public int hashCode() {
        int result = fromNodeId.hashCode();
        result = 31 * result + fromPortId.hashCode();
        result = 31 * result + toNodeId.hashCode();
        result = 31 * result + toPortId.hashCode();
        return 31 * result + interfaceKind.hashCode();
    }
}
