package darkgrey.rpg.client.session;

import java.text.BreakIterator;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** Elapsed-time reveal; boundaries never split a surrogate pair or combining character. */
public final class DialogueTextReveal {

    private String text = "";
    private int[] boundaries = new int[0];
    private long started;
    private double speed;
    private boolean complete = true;
    private int retained;

    public void begin(String value, double charactersPerSecond, long now) {
        text = value == null ? "" : value;
        speed = charactersPerSecond;
        started = now;
        complete = speed == 0;
        retained = 0;
        BreakIterator iterator = BreakIterator.getCharacterInstance(Locale.ROOT);
        iterator.setText(text);
        List<Integer> ends = new ArrayList<Integer>();
        iterator.first();
        for (int end = iterator.next(); end != BreakIterator.DONE; end = iterator.next()) {
            if (end < text.length() && text.charAt(end - 1) == '\u00a7') continue;
            ends.add(end);
        }
        boundaries = new int[ends.size()];
        for (int i = 0; i < boundaries.length; i++) boundaries[i] = ends.get(i);
    }

    public String visible(long now) {
        if (complete) return text;
        int count = (int) Math.min(boundaries.length, Math.max(0, (now - started) / 1000000000.0 * speed));
        int end = count == 0 ? 0 : boundaries[count - 1];
        return text.substring(0, Math.max(retained, end));
    }

    public void retain(int characters) {
        retained = Math.max(0, Math.min(text.length(), characters));
        int count = 0;
        while (count < boundaries.length && boundaries[count] <= retained) count++;
        if (speed > 0) started -= (long) (count / speed * 1000000000.0);
    }

    public void speed(double value, long now) {
        if (value == speed) return;
        int shown = visible(now).length();
        begin(text, value, now);
        retain(shown);
        int count = 0;
        while (count < boundaries.length && boundaries[count] <= shown) count++;
        if (value > 0) started = now - (long) (count / value * 1000000000.0);
    }

    public boolean finish(long now) {
        boolean changed = visible(now).length() < text.length();
        complete = true;
        return changed;
    }
}
