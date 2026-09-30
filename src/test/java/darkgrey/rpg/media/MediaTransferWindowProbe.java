package darkgrey.rpg.media;

import java.util.*;

import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;

public final class MediaTransferWindowProbe {

    static final String REF = "media/" + String.join("", Collections.nCopies(64, "a")) + ".png";

    static CanonicalMediaChunk chunk(int offset, int total) {
        return new CanonicalMediaChunk(1, REF, total, offset, new byte[Math.min(32768, total - offset)]);
    }

    static void check(boolean ok, String message) {
        if (!ok) throw new AssertionError(message);
    }

    public static void main(String[] args) {
        MediaTransferWindow w = new MediaTransferWindow();
        int total = 32768 * 96 + 23;
        check(
            w.requests(1)
                .equals(Arrays.asList(0)),
            "unknown total must probe only first chunk");
        check(w.accept(chunk(0, total)), "first response");
        List<Integer> requests = w.requests(2);
        check(requests.size() == 7 && w.retained() == 8, "first window bound");
        check(!w.accept(chunk(32768 * 20, total)), "unsolicited offset accepted");
        Collections.reverse(requests);
        for (int offset : requests) w.accept(chunk(offset, total));
        List<CanonicalMediaChunk> first = w.take();
        check(first.size() == 8, "reorder drain");
        check(
            w.requests(3_000_000_000L)
                .isEmpty(),
            "writing chunks retried");
        w.written();
        requests = w.requests(3_000_000_001L);
        check(requests.size() == 8, "window refill");
        for (int i = 1; i < requests.size(); i++) w.accept(chunk(requests.get(i), total));
        check(
            w.take()
                .isEmpty(),
            "gap bypassed");
        check(
            w.requests(6_000_000_001L)
                .equals(Arrays.asList(requests.get(0))),
            "retry must request only missing chunk");
        w.accept(chunk(requests.get(0), total));
        check(!w.accept(chunk(requests.get(0), total)), "duplicate buffered");
        check(
            w.take()
                .size() == 8,
            "retry gap not repaired");
        w.written();
        int bytes = 32768 * 16;
        while (bytes < total) {
            requests = w.requests(7_000_000_000L);
            check(requests.size() <= 8, "window exceeded");
            Collections.reverse(requests);
            for (int offset : requests) w.accept(chunk(offset, total));
            for (CanonicalMediaChunk c : w.take()) bytes += c.getData().length;
            w.written();
        }
        check(
            bytes == total && w.retained() == 0
                && w.requests(Long.MAX_VALUE)
                    .isEmpty(),
            "completion leak");
        MediaTransferWindow bad = new MediaTransferWindow();
        bad.requests(0);
        bad.accept(chunk(0, total));
        bad.requests(0);
        boolean rejected = false;
        try {
            bad.accept(chunk(32768, total - 1));
        } catch (IllegalArgumentException expected) {
            rejected = true;
        }
        check(rejected, "inconsistent total accepted");
        bad.clear();
        check(
            bad.retained() == 0 && bad.take()
                .isEmpty(),
            "retired window retains payloads");
        System.out.println("MEDIA_WINDOW_BOUNDS_REORDER_DUPLICATE_RETRY_WRITING_TOTAL=PASS");
    }
}
