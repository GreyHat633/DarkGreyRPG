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

    public void begin(String value, double charactersPerSecond, long now) {
        text = value == null ? "" : value;
        speed = charactersPerSecond;
        started = now;
        complete = speed == 0;
        BreakIterator iterator = BreakIterator.getCharacterInstance(Locale.ROOT);
        iterator.setText(text);
        List<Integer> ends = new ArrayList<Integer>();
        iterator.first();
        for (int end = iterator.next(); end != BreakIterator.DONE; end = iterator.next()) ends.add(end);
        boundaries = new int[ends.size()];
        for (int i = 0; i < boundaries.length; i++) boundaries[i] = ends.get(i);
    }

    public String visible(long now) {
        if (complete) return text;
        int count = (int) Math.min(boundaries.length, Math.max(0, (now - started) / 1000000000.0 * speed));
        return count == 0 ? "" : text.substring(0, boundaries[count - 1]);
    }

    public boolean finish(long now) {
        boolean changed = visible(now).length() < text.length();
        complete = true;
        return changed;
    }
}
