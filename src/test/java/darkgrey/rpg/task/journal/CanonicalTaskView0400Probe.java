package darkgrey.rpg.task.journal;

import darkgrey.rpg.network.message.canonical.CanonicalTaskViewOpen;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Strict single-current read-only menu signal; no task data or mutations are transported here. */
public final class CanonicalTaskView0400Probe {

    private CanonicalTaskView0400Probe() {}

    public static void main(String[] args) {
        for (int dimension : new int[] { -1, 0, 1, 42 }) {
            ByteBuf bytes = Unpooled.buffer();
            try {
                new CanonicalTaskViewOpen(dimension).toBytes(bytes);
                CanonicalTaskViewOpen decoded = new CanonicalTaskViewOpen();
                decoded.fromBytes(bytes);
                if (decoded.getDimension() != dimension || bytes.isReadable())
                    throw new AssertionError("dimension round trip");
                bytes.clear();
                bytes.writeInt(0x44475236)
                    .writeInt(dimension);
                reject(bytes);
                bytes.clear();
                bytes.writeInt(CanonicalTaskViewOpen.PROTOCOL_MARKER)
                    .writeInt(dimension)
                    .writeByte(0);
                reject(bytes);
                bytes.clear();
                bytes.writeInt(CanonicalTaskViewOpen.PROTOCOL_MARKER);
                reject(bytes);
            } finally {
                bytes.release();
            }
        }
        System.out.println("CANONICAL_TASK_VIEW_CURRENT_MARKER_DIMENSION_REJECTION=PASS");
    }

    private static void reject(ByteBuf bytes) {
        try {
            new CanonicalTaskViewOpen().fromBytes(bytes);
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Invalid current menu signal accepted");
    }
}
