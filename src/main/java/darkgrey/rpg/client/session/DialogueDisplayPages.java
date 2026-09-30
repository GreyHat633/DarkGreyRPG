package darkgrey.rpg.client.session;

import java.text.BreakIterator;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/** Local display geometry only. Every line retains its exact source range. */
public final class DialogueDisplayPages {

    public interface Metrics {

        double advance(String cluster, boolean bold);
    }

    public static final class Line {

        public final int start, end;
        public final String format;

        Line(int start, int end, String format) {
            this.start = start;
            this.end = end;
            this.format = format;
        }
    }

    public final List<Line> lines;
    public final int linesPerPage;

    private DialogueDisplayPages(List<Line> lines, int count) {
        this.lines = Collections.unmodifiableList(lines);
        linesPerPage = Math.max(1, count);
    }

    public int pageCount() {
        return Math.max(1, (lines.size() + linesPerPage - 1) / linesPerPage);
    }

    public int start(int page) {
        return lines.get(page * linesPerPage).start;
    }

    public int end(int page) {
        return lines.get(Math.min(lines.size(), (page + 1) * linesPerPage) - 1).end;
    }

    public int pageAt(int offset) {
        for (int page = 0; page < pageCount(); page++) if (offset < end(page)) return page;
        return pageCount() - 1;
    }

    public static DialogueDisplayPages measure(String text, double width, int rows, Metrics metrics) {
        List<Line> lines = new ArrayList<Line>();
        BreakIterator breaks = BreakIterator.getCharacterInstance(Locale.ROOT);
        breaks.setText(text);
        int start = 0, at = 0;
        double used = 0;
        boolean bold = false;
        String format = "", lineFormat = "";
        width = Math.max(1, width);
        while (at < text.length()) {
            int end = breaks.following(at);
            if (end == BreakIterator.DONE) end = text.length();
            if (text.charAt(at) == '\u00a7' && at + 1 < text.length()) {
                end = at + 2;
                char code = Character.toLowerCase(text.charAt(at + 1));
                if ("0123456789abcdefr".indexOf(code) >= 0) {
                    bold = false;
                    format = code == 'r' ? "" : text.substring(at, end);
                } else if ("klmno".indexOf(code) >= 0) {
                    if (code == 'l') bold = true;
                    if (format.indexOf("\u00a7" + code) < 0) format += "\u00a7" + code;
                }
                at = end;
                continue;
            }
            String cluster = text.substring(at, end);
            boolean newline = cluster.indexOf('\n') >= 0 || cluster.indexOf('\r') >= 0;
            double advance = newline ? 0 : Math.max(0, metrics.advance(cluster, bold));
            if (!newline && used > 0 && used + advance > width) {
                lines.add(new Line(start, at, lineFormat));
                start = at;
                used = 0;
                lineFormat = format;
            }
            used += advance;
            at = end;
            if (newline) {
                lines.add(new Line(start, at, lineFormat));
                start = at;
                used = 0;
                lineFormat = format;
            }
        }
        if (start < text.length() || lines.isEmpty()) lines.add(new Line(start, text.length(), lineFormat));
        return new DialogueDisplayPages(lines, rows);
    }
}
