package darkgrey.rpg.session.server;

import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Immutable detached result of one server-side Session orchestration operation. */
public final class CanonicalSessionDispatch {

    private final CanonicalSessionFrame frame;
    private final CanonicalSessionClose close;
    private final CanonicalSessionCompletionResult completionResult;

    private CanonicalSessionDispatch(CanonicalSessionFrame frame, CanonicalSessionClose close,
        CanonicalSessionCompletionResult completionResult) {
        if ((frame == null) == (close == null) || (close == null) != (completionResult == null))
            throw new IllegalArgumentException("A Session dispatch must be either a frame or a completion.");
        this.frame = frame == null ? null : copy(frame);
        this.close = close == null ? null : copy(close);
        this.completionResult = completionResult;
    }

    public static CanonicalSessionDispatch frame(CanonicalSessionFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Session frame is required.");
        return new CanonicalSessionDispatch(frame, null, null);
    }

    public static CanonicalSessionDispatch completed(CanonicalSessionClose close,
        CanonicalSessionCompletionResult completionResult) {
        return new CanonicalSessionDispatch(null, close, completionResult);
    }

    public CanonicalSessionFrame getFrame() {
        return frame == null ? null : copy(frame);
    }

    public CanonicalSessionFrame getSessionFrame() {
        return frame == null ? null : copy(frame);
    }

    public CanonicalSessionClose getClose() {
        return close == null ? null : copy(close);
    }

    public CanonicalSessionClose getSessionClose() {
        return close == null ? null : copy(close);
    }

    public CanonicalSessionCompletionResult getCompletionResult() {
        return completionResult;
    }

    public CanonicalSessionCompletionResult getCompletion() {
        return completionResult;
    }

    public CanonicalSessionCompletionResult getResult() {
        return completionResult;
    }

    public boolean isFrame() {
        return frame != null;
    }

    public boolean isCompleted() {
        return completionResult != null;
    }

    public boolean isClosed() {
        return close != null;
    }

    private static CanonicalSessionFrame copy(CanonicalSessionFrame source) {
        ByteBuf buffer = Unpooled.buffer();
        try {
            source.toBytes(buffer);
            CanonicalSessionFrame result = new CanonicalSessionFrame();
            result.fromBytes(buffer);
            return result;
        } finally {
            buffer.release();
        }
    }

    private static CanonicalSessionClose copy(CanonicalSessionClose source) {
        ByteBuf buffer = Unpooled.buffer();
        try {
            source.toBytes(buffer);
            CanonicalSessionClose result = new CanonicalSessionClose();
            result.fromBytes(buffer);
            return result;
        } finally {
            buffer.release();
        }
    }
}
