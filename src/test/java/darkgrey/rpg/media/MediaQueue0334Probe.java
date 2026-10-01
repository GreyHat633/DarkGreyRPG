package darkgrey.rpg.media;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

public final class MediaQueue0334Probe {

    public static void main(String[] args) {
        Map<String, Candidate> states = new HashMap<String, Candidate>();
        List<String> refs = Arrays.asList("failed", "background", "visible");
        Set<String> visible = Collections.singleton("visible");
        Candidate failed = new Candidate();
        failed.retry = 5000;
        states.put("failed", failed);
        check(
            "visible".equals(MediaDownloadSelection.next(refs, visible, true, 100, states::get)),
            "backoff blocks visible");
        Candidate active = new Candidate();
        active.busy = true;
        states.put("visible", active);
        check(MediaDownloadSelection.next(refs, visible, true, 200, states::get) == null, "second file admitted");
        active.busy = false;
        active.ready = true;
        check(
            "background".equals(MediaDownloadSelection.next(refs, visible, true, 200, states::get)),
            "background starved");
        check(
            "failed".equals(MediaDownloadSelection.next(refs, visible, true, 6000, states::get)),
            "retry not admitted");
        check(
            MediaDownloadSelection.next(refs, visible, false, 200, states::get) == null,
            "standalone frame preloaded");
        MediaUploadBudget budget = new MediaUploadBudget();
        for (int i = 0; i < 4; i++) check(budget.reserve(4194304L), "reservation rejected");
        check(!budget.reserve(1), "upload queue unbounded");
        check(budget.pixels() == 16777216L, "wrong pixel budget");
        budget.release(4194304L);
        check(budget.reserve(4194304L), "released budget not reusable");
        for (int i = 0; i < 4; i++) budget.release(4194304L);
        check(budget.count() == 0 && budget.pixels() == 0, "reservation leak");
        check(!budget.reserve(16777217L), "pixel overflow");
        System.out.println("MEDIA_QUEUE_FAILURE_VISIBLE_PRIORITY_SINGLE_FILE_UPLOAD_BOUNDS=PASS");
    }

    private static class Candidate implements MediaDownloadSelection.State {

        boolean ready, busy;
        long retry;

        public boolean ready() {
            return ready;
        }

        public boolean busy() {
            return busy;
        }

        public long retryAt() {
            return retry;
        }
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
