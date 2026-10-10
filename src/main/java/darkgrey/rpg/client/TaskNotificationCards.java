package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.opengl.GL11;

import darkgrey.rpg.client.gui.DgrUiPalette;
import darkgrey.rpg.client.gui.DialogueFontDrawing;

/** Client presentation only; authoritative task transitions arrive from the server. */
public final class TaskNotificationCards {

    private static final long SLIDE = 600000000L, READ = 4000000000L;
    private static final int TITLE_LINE_HEIGHT = 11;
    private static final int HEIGHT = 95, GAP = 5;
    private static int visibleSlots = 3;
    private static final LinkedHashMap<String, Card> cards = new LinkedHashMap<String, Card>();
    private static final LinkedHashMap<String, Card> pending = new LinkedHashMap<String, Card>();
    private static final LinkedHashSet<String> seen = new LinkedHashSet<String>();

    private TaskNotificationCards() {}

    public static synchronized void clear() {
        cards.clear();
        pending.clear();
        seen.clear();
        visibleSlots = 3;
    }

    public static synchronized void accept(NBTTagList events, long now) {
        // Merge before expiry: an update received during exit revives the same card.
        LinkedHashMap<String, List<NBTTagCompound>> batches = new LinkedHashMap<String, List<NBTTagCompound>>();
        for (int i = 0; i < events.tagCount(); i++) {
            NBTTagCompound event = events.getCompoundTagAt(i);
            String id = event.getString("event"), task = event.getString("task"), kind = event.getString("kind");
            if (id.isEmpty() || task.isEmpty()
                || !(kind.equals("received") || kind.equals("completed")
                    || kind.equals("failed")
                    || kind.equals("objective_active")
                    || kind.equals("objective_completed")))
                continue;
            if (!seen.add(id)) continue;
            while (seen.size() > 2048) seen.remove(
                seen.iterator()
                    .next());
            List<NBTTagCompound> batch = batches.get(task);
            if (batch == null) {
                batch = new ArrayList<NBTTagCompound>();
                batches.put(task, batch);
            }
            batch.add(event);
        }
        for (Map.Entry<String, List<NBTTagCompound>> batch : batches.entrySet()) {
            Card card = cards.get(batch.getKey());
            boolean visible = card != null;
            if (card == null) card = pending.get(batch.getKey());
            if (card == null) {
                card = new Card();
                pending.put(batch.getKey(), card);
            }
            card.merge(batch.getValue());
            if (visible) card.refresh(now);
        }
        advance(now);
    }

    private static void advance(long now) {
        Iterator<Card> it = cards.values()
            .iterator();
        while (it.hasNext()) if (now >= it.next().exitAt + SLIDE) it.remove();
        while (cards.size() < visibleSlots && !pending.isEmpty()) {
            String key = pending.keySet()
                .iterator()
                .next();
            Card card = pending.remove(key);
            card.entered = now;
            card.from = 1;
            card.exitAt = now + SLIDE + READ;
            double bottom = 0;
            for (Card visible : cards.values()) bottom += visible.height + GAP;
            card.yFrom = card.yTarget = bottom;
            card.yAt = now;
            cards.put(key, card);
        }
        reflow(now);
    }

    private static void reflow(long now) {
        int nextY = 0;
        for (Card card : cards.values()) {
            double target = nextY;
            nextY += card.height + GAP;
            if (card.yTarget != target) {
                card.yFrom = card.y(now);
                card.yTarget = target;
                card.yAt = now;
            }
        }
    }

    private static double ease(double t) {
        t = Math.max(0, Math.min(1, t));
        return t * t * (3 - 2 * t);
    }

    /** Right edge to resting position, with time-based acceleration and deceleration. */
    public static int slideOffset(int distance, long elapsedNanos) {
        return (int) Math.round(distance * (1 - ease(elapsedNanos / (double) SLIDE)));
    }

    public static synchronized void draw() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null) return;
        long now = System.nanoTime();
        ScaledResolution resolution = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight);
        int screenWidth = resolution.getScaledWidth();
        boolean leftSide = darkgrey.rpg.client.session.PlayerUiPreferences.notificationSide()
            == darkgrey.rpg.client.session.PlayerUiPreferences.Side.LEFT;
        int width = Math.min(190, screenWidth / 2), x = leftSide ? 8 : screenWidth - width - 8;
        prepareFrame(now, resolution.getScaledHeight(), width - 14, value -> mc.fontRenderer.getStringWidth(value));
        GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
        GL11.glPushMatrix();
        try {
            GL11.glDisable(GL11.GL_SCISSOR_TEST);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
            GL11.glColor4f(1, 1, 1, 1);
            for (Card card : cards.values()) {
                int left = x + (leftSide ? -1 : 1) * (int) Math.round((width + 8) * card.offset(now));
                int top = 12 + (int) Math.round(card.y(now));
                // Cards overlap other HUDs; their text must not show through this foreground surface.
                Gui.drawRect(
                    left,
                    top,
                    left + width,
                    top + card.height,
                    0xFF000000 | (DgrUiPalette.WINDOW_PANEL & 0xFFFFFF));
                Gui.drawRect(left, top, left + width, top + 1, DgrUiPalette.SELECTED_BORDER);
                Gui.drawRect(left, top + card.height - 1, left + width, top + card.height, DgrUiPalette.BORDER);
                Gui.drawRect(left, top, left + 1, top + card.height, DgrUiPalette.BORDER);
                Gui.drawRect(left + width - 1, top, left + width, top + card.height, DgrUiPalette.BORDER);
                small(mc.fontRenderer, card.label, left + 7, top + 6, width - 14);
                int textY = top + 17;
                for (String line : card.titleLines) {
                    DialogueFontDrawing.draw(mc.fontRenderer, line, left + 7, textY, 1, DgrUiPalette.TEXT);
                    textY += TITLE_LINE_HEIGHT;
                }
                if (!card.bodyLines.isEmpty()) {
                    Gui.drawRect(left + 7, textY + 2, left + width - 7, textY + 3, DgrUiPalette.BORDER);
                    textY += 7;
                    if (!card.completed.isEmpty() && card.terminal.isEmpty() && !card.active.isEmpty()) {
                        small(mc.fontRenderer, "已完成：" + card.completed, left + 7, textY, width - 14);
                        textY += 10;
                    }
                    for (String line : card.bodyLines) {
                        DialogueFontDrawing.draw(mc.fontRenderer, line, left + 7, textY, 1, DgrUiPalette.TEXT);
                        textY += 10;
                    }
                }
                if (card.active.size() > 1 && card.terminal.isEmpty()) {
                    small(mc.fontRenderer, "另有 " + (card.active.size() - 1) + " 个新目标", left + 7, textY + 2, width - 14);
                    textY += 10;
                }

            }
        } finally {
            GL11.glPopMatrix();
            GL11.glPopAttrib();
        }
    }

    /** Admit and expire before layout; drawing and reflow cannot add an unlaid-out card. */
    private static void prepareFrame(long now, int screenHeight, int textWidth, TextWidth measure) {
        // Keep glyphs at native size. A short viewport queues extra cards instead of shrinking their text.
        // The tallest card is 91 GUI units: two title/body lines and both objective summaries.
        visibleSlots = Math.max(1, Math.min(3, (screenHeight - 24) / (91 + GAP)));
        while (cards.size() > visibleSlots) {
            String last = null;
            for (String key : cards.keySet()) last = key;
            LinkedHashMap<String, Card> deferred = new LinkedHashMap<String, Card>();
            deferred.put(last, cards.remove(last));
            deferred.putAll(pending);
            pending.clear();
            pending.putAll(deferred);
        }
        advance(now);
        for (Card card : cards.values()) {
            card.titleLines = wrap(card.title, textWidth, 2, measure);
            card.bodyLines = card.body()
                .isEmpty() ? Collections.<String>emptyList() : wrap(card.body(), textWidth, 2, measure);
            card.height = 17 + card.titleLines.size() * TITLE_LINE_HEIGHT + 5;
            if (!card.bodyLines.isEmpty()) card.height += 7 + card.bodyLines.size() * 10;
            if (card.active.size() > 1 && card.terminal.isEmpty()) card.height += 10;
            if (!card.completed.isEmpty() && card.terminal.isEmpty() && !card.active.isEmpty()) card.height += 10;
        }
        reflow(now);
    }

    private static void small(FontRenderer font, String value, int x, int y, int width) {
        DialogueFontDrawing.draw(font, lines(font, value, width, 1).get(0), x, y, 1, DgrUiPalette.SECONDARY);
    }

    private static List<String> lines(final FontRenderer font, String text, int width, int limit) {
        return wrap(text, width, limit, new TextWidth() {

            public int width(String value) {
                return font.getStringWidth(value);
            }
        });
    }

    /** Code point-safe bounded wrapping, shared by renderer and deterministic layout tests. */
    public interface TextWidth {

        int width(String text);
    }

    public static List<String> wrap(String text, int width, int limit, TextWidth measure) {
        List<String> result = new ArrayList<String>();
        String prefix = text.startsWith("\u00a7l") ? "\u00a7l" : "";
        int pos = prefix.length();
        while (result.size() < limit && pos < text.length()) {
            String line = prefix;
            while (pos < text.length()) {
                int cp = text.codePointAt(pos), next = pos + Character.charCount(cp);
                if (cp == '\n') {
                    pos = next;
                    break;
                }
                String candidate = line + new String(Character.toChars(cp));
                if (measure.width(candidate) > width && line.length() > prefix.length()) break;
                line = candidate;
                pos = next;
            }
            if (result.size() == limit - 1 && pos < text.length()) {
                while (line.length() > prefix.length() && measure.width(line + "…") > width)
                    line = line.substring(0, line.offsetByCodePoints(line.length(), -1));
                line += "…";
            }
            result.add(line);
        }
        if (result.isEmpty()) result.add("");
        return result;
    }

    private static final class Card {

        String label = "任务更新", title = "", completed = "", terminal = "";
        final LinkedHashMap<String, String> active = new LinkedHashMap<String, String>();
        int height = HEIGHT;
        List<String> titleLines, bodyLines;
        long entered, exitAt, yAt;
        double from = 1, yFrom, yTarget;

        void merge(List<NBTTagCompound> batch) {
            boolean received = false;
            LinkedHashMap<String, String> done = new LinkedHashMap<String, String>();
            for (NBTTagCompound event : batch) {
                title = event.getString("title");
                String kind = event.getString("kind"), id = event.getString("objective"),
                    text = event.getString("text");
                if (kind.equals("received")) received = true;
                if (kind.equals("completed")) terminal = "任务完成";
                if (kind.equals("failed")) terminal = "任务失败";
                if (kind.equals("objective_active")) active.put(id, text);
                if (kind.equals("objective_completed")) done.put(id, text);
            }
            for (Map.Entry<String, String> entry : done.entrySet()) {
                active.remove(entry.getKey());
                completed = entry.getValue();
            }
            if (!terminal.isEmpty()) {
                active.clear();
                completed = "";
                label = terminal;
            } else label = received ? "任务接受" : "任务更新";
        }

        String body() {
            if (!terminal.isEmpty()) return terminal;
            if (!active.isEmpty()) return "新目标：" + active.values()
                .iterator()
                .next();
            return completed.isEmpty() ? "" : "已完成：" + completed;
        }

        double offset(long now) {
            if (now < entered + SLIDE) return from * (1 - ease((now - entered) / (double) SLIDE));
            return now < exitAt ? 0 : ease((now - exitAt) / (double) SLIDE);
        }

        void refresh(long now) {
            if (now >= exitAt) {
                from = offset(now);
                entered = now;
            }
            exitAt = Math.max(now, entered + SLIDE) + READ;
        }

        double y(long now) {
            return yFrom + (yTarget - yFrom) * ease((now - yAt) / (double) SLIDE);
        }
    }
}
