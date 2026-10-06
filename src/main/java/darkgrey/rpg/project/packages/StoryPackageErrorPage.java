package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Bounded chat pages of the latest validation report; complete messages remain in the log. */
public final class StoryPackageErrorPage {

    private static final int PAGE_LINES = 8;
    private static final int LINE_CHARACTERS = 160;
    private final int page, pageCount;
    private final List<String> lines;

    public StoryPackageErrorPage(List<String> errors, int page) {
        List<String> all = new ArrayList<String>();
        for (String error : errors) {
            if (error == null) continue;
            for (String line : error.split("\n")) {
                String safe = sanitize(line);
                if (safe.trim()
                    .isEmpty()) continue;
                int start = 0;
                while (start < safe.length()) {
                    int end = Math.min(safe.length(), start + LINE_CHARACTERS);
                    if (end < safe.length() && Character.isHighSurrogate(safe.charAt(end - 1))) end--;
                    all.add((start == 0 ? "" : "  ") + safe.substring(start, end));
                    start = end;
                }
            }
        }
        pageCount = Math.max(1, (all.size() + PAGE_LINES - 1) / PAGE_LINES);
        if (page < 1 || page > pageCount) throw new IllegalArgumentException("页码应为 1～" + pageCount + "。");
        this.page = page;
        int start = Math.min(all.size(), (page - 1) * PAGE_LINES);
        lines = Collections
            .unmodifiableList(new ArrayList<String>(all.subList(start, Math.min(all.size(), start + PAGE_LINES))));
    }

    private static String sanitize(String line) {
        StringBuilder safe = new StringBuilder();
        for (int i = 0; i < line.length(); i++) {
            char c = line.charAt(i);
            safe.append(c == '\u00a7' ? '?' : Character.isISOControl(c) ? ' ' : c);
        }
        return safe.toString();
    }

    public int getPage() {
        return page;
    }

    public int getPageCount() {
        return pageCount;
    }

    public List<String> getLines() {
        return lines;
    }
}
