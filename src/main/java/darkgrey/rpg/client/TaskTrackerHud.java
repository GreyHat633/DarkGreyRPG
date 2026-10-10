package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.client.gui.CanonicalDialogueRenderer;
import darkgrey.rpg.client.gui.DgrUiPalette;
import darkgrey.rpg.client.gui.DialogueFontDrawing;
import darkgrey.rpg.client.gui.ItemSlotStrip;
import darkgrey.rpg.client.gui.TaskObjectiveText;
import darkgrey.rpg.client.gui.TaskStoryRows;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Compact story/task/objective blocks; full details remain in the journal. */
public final class TaskTrackerHud {

    private static final List<Block> blocks = new ArrayList<Block>();
    private static long snapshotRevision = Long.MIN_VALUE, selectionRevision = Long.MIN_VALUE, resourceRevision = -1;
    private static int wrapWidth;
    private static double scale;

    private TaskTrackerHud() {}

    private static final class Row {

        final String text;
        final int role;
        final ItemSlotStrip icon;

        Row(String text, int role, ItemSlotStrip icon) {
            this.text = text;
            this.role = role;
            this.icon = icon;
        }
    }

    private static final class Block {

        String story, storyTitle;
        String title;
        final List<Row> rows = new ArrayList<Row>();
    }

    private static String fit(Minecraft mc, String value, int width) {
        String text = value.replace("§l", "")
            .replace("§r", "");
        if (mc.fontRenderer.getStringWidth(text) <= width) return text;
        String trimmed = mc.fontRenderer
            .trimStringToWidth(text, Math.max(0, width - mc.fontRenderer.getStringWidth("…")));
        if (!trimmed.isEmpty() && Character.isHighSurrogate(trimmed.charAt(trimmed.length() - 1)))
            trimmed = trimmed.substring(0, trimmed.length() - 1);
        if (trimmed.endsWith("§")) trimmed = trimmed.substring(0, trimmed.length() - 1);
        return trimmed + "…";
    }

    private static int rowHeight(Row row, Minecraft mc) {
        return row.text.isEmpty() ? 2
            : row.icon == null ? (int) Math.ceil(mc.fontRenderer.FONT_HEIGHT * scale) + 2
                : (int) Math.ceil(20 * scale) + 2;
    }

    private static void rebuild(Minecraft mc) {
        blocks.clear();
        NBTTagList tasks = CanonicalTaskClientStore.getSnapshot()
            .getTagList("tasks", 10);
        NBTTagList tracked = new NBTTagList();
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            if (TaskTrackerClient.selected()
                .contains(TaskTrackerClient.identity(task))) tracked.appendTag(task.copy());
        }
        Map<String, String> names = new LinkedHashMap<String, String>();
        for (NBTTagCompound header : TaskStoryRows.flatten(tracked, java.util.Collections.<String>emptySet()))
            names.put(TaskStoryRows.key(header), header.getString("title"));
        for (String story : names.keySet()) for (int i = 0; i < tracked.tagCount(); i++) {
            NBTTagCompound task = tracked.getCompoundTagAt(i);
            if (!story.equals(TaskStoryRows.key(task))) continue;
            Block block = new Block();
            block.story = story;
            block.storyTitle = fit(mc, names.get(story), wrapWidth - 12);
            block.title = fit(mc, task.getString("title"), wrapWidth - 12);
            NBTTagList objectives = task.getTagList("objectives", 10);
            for (int j = 0; j < objectives.tagCount(); j++) {
                NBTTagCompound chosen = objectives.getCompoundTagAt(j);
                List<Row> rows = new ArrayList<Row>();
                String description = chosen.getString("text");
                List<String> wrapped = mc.fontRenderer
                    .listFormattedStringToWidth(description, Math.max(1, wrapWidth - 6));
                for (int k = 0; k < wrapped.size(); k++) rows.add(new Row(wrapped.get(k), k == 0 ? 2 : 3, null));
                for (String text : TaskObjectiveText.lines(chosen))
                    if (text.startsWith("持有 ") || text.startsWith("进度 ")) {
                        ItemSlotStrip icon = null;
                        if (chosen.hasKey("item_preview", 10)) {
                            NBTTagCompound preview = (NBTTagCompound) chosen.getCompoundTag("item_preview")
                                .copy();
                            preview.removeTag("notice");
                            icon = new ItemSlotStrip(preview, true);
                        }
                        rows.add(new Row(fit(mc, text, Math.max(1, wrapWidth - (icon == null ? 6 : 24))), 3, icon));
                        break;
                    }
                block.rows.addAll(rows);
                if (j + 1 < objectives.tagCount()) block.rows.add(new Row("", 3, null));
            }
            blocks.add(block);
        }
    }

    public static void draw() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null) return;
        ScaledResolution resolution = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight);
        int width = resolution.getScaledWidth(), height = resolution.getScaledHeight(), right = width - 8;
        boolean leftSide = PlayerUiPreferences.trackerSide() == PlayerUiPreferences.Side.LEFT;
        net.minecraft.scoreboard.ScoreObjective scoreboard = mc.theWorld.getScoreboard()
            .func_96539_a(1);
        if (scoreboard != null && !leftSide) {
            int occupied = mc.fontRenderer.getStringWidth(scoreboard.getDisplayName()) + 36;
            for (Object object : mc.theWorld.getScoreboard()
                .func_96534_i(scoreboard)) {
                net.minecraft.scoreboard.Score score = (net.minecraft.scoreboard.Score) object;
                String player = net.minecraft.scoreboard.ScorePlayerTeam.formatPlayerName(
                    mc.theWorld.getScoreboard()
                        .getPlayersTeam(score.getPlayerName()),
                    score.getPlayerName());
                occupied = Math
                    .max(occupied, mc.fontRenderer.getStringWidth(player + ": " + score.getScorePoints()) + 36);
            }
            right -= occupied;
        }
        int panelWidth = Math.min(Math.min(200, width / 4), right - 8);
        if (panelWidth < 30) return;
        double nextScale = PlayerUiPreferences.textScale();
        int nextWrap = Math.max(1, (int) ((panelWidth - 24) / nextScale));
        if (snapshotRevision != CanonicalTaskClientStore.getRevision()
            || selectionRevision != TaskTrackerClient.revision()
            || resourceRevision != ClientResourceRevision.current()
            || scale != nextScale
            || wrapWidth != nextWrap) {
            snapshotRevision = CanonicalTaskClientStore.getRevision();
            selectionRevision = TaskTrackerClient.revision();
            resourceRevision = ClientResourceRevision.current();
            scale = nextScale;
            wrapWidth = nextWrap;
            rebuild(mc);
        }
        if (blocks.isEmpty()) return;
        int lineHeight = (int) Math.ceil(mc.fontRenderer.FONT_HEIGHT * scale) + 2;
        int[] shown = new int[blocks.size()];
        int count = blocks.size();
        int fixedHeight = layoutHeight(mc, shown, count, lineHeight);
        // Reserve every story/task header and every overflow marker first, then share body space equally.
        int available = Math.min(height - 16, Math.max(height / 3, fixedHeight + count * 44));
        int bodyBudget = Math.max(lineHeight, (available - fixedHeight + count * lineHeight) / count);
        for (int i = 0; i < blocks.size(); i++) {
            List<Row> rows = blocks.get(i).rows;
            int[] heights = new int[rows.size()];
            for (int j = 0; j < rows.size(); j++) heights[j] = rowHeight(rows.get(j), mc);
            shown[i] = TaskTrackerBodyLayout.visibleRows(heights, bodyBudget, lineHeight);
        }
        int used = layoutHeight(mc, shown, count, lineHeight);
        int naturalWidth = 0;
        for (int i = 0; i < count; i++) {
            Block block = blocks.get(i);
            naturalWidth = Math.max(naturalWidth, mc.fontRenderer.getStringWidth(block.storyTitle) + 12);
            naturalWidth = Math.max(naturalWidth, mc.fontRenderer.getStringWidth(block.title) + 12);
            for (int j = 0; j < shown[i]; j++) {
                Row row = block.rows.get(j);
                naturalWidth = Math
                    .max(naturalWidth, mc.fontRenderer.getStringWidth(row.text) + (row.icon == null ? 6 : 24));
            }
            if (shown[i] < block.rows.size())
                naturalWidth = Math.max(naturalWidth, mc.fontRenderer.getStringWidth(fit(mc, "! 部分内容已省略", wrapWidth)));
        }
        panelWidth = Math.min(panelWidth, (int) Math.ceil(naturalWidth * scale) + 24);
        if (leftSide) right = 8 + panelWidth;
        int left = right - panelWidth, top = Math.max(4, (height - used) / 2), y = top;
        org.lwjgl.opengl.GL11.glPushAttrib(org.lwjgl.opengl.GL11.GL_ALL_ATTRIB_BITS);
        try {
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_SCISSOR_TEST);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
            org.lwjgl.opengl.GL11.glColor4f(1, 1, 1, 1);
            int index = 0;
            while (index < count) {
                int end = index + 1;
                while (end < count && blocks.get(end).story.equals(blocks.get(index).story)) end++;
                int storyHeight = 8 + lineHeight;
                for (int i = index; i < end; i++)
                    storyHeight += taskHeight(mc, blocks.get(i), shown[i], lineHeight) + 4;
                box(left, y, right, y + storyHeight, 0xEB000000 | (DgrUiPalette.WINDOW_PANEL & 0xFFFFFF));
                glyph(left + 6, y + 5, true, DgrUiPalette.STORY_TEXT);
                CanonicalDialogueRenderer.drawText(
                    mc.fontRenderer,
                    blocks.get(index).storyTitle,
                    left + 18,
                    y + 4,
                    scale,
                    DgrUiPalette.STORY_TEXT);
                int taskY = y + 4 + lineHeight;
                for (int i = index; i < end; i++) {
                    Block block = blocks.get(i);
                    int taskHeight = taskHeight(mc, block, shown[i], lineHeight);
                    box(left + 4, taskY, right - 4, taskY + taskHeight, DgrUiPalette.SUB_PANEL);
                    glyph(left + 8, taskY + 5, false, DgrUiPalette.TEXT);
                    CanonicalDialogueRenderer
                        .drawText(mc.fontRenderer, block.title, left + 20, taskY + 4, scale, DgrUiPalette.TEXT);
                    int rowY = taskY + 4 + lineHeight;
                    for (int j = 0; j < shown[i]; j++) {
                        Row row = block.rows.get(j);
                        if (row.icon == null) {
                            if (row.role == 2) Gui.drawRect(left + 9, rowY + 4, left + 11, rowY + 6, DgrUiPalette.TEXT);
                            CanonicalDialogueRenderer
                                .drawText(mc.fontRenderer, row.text, left + 14, rowY, scale, DgrUiPalette.TEXT);
                        } else {
                            org.lwjgl.opengl.GL11.glPushMatrix();
                            try {
                                org.lwjgl.opengl.GL11.glTranslated(left + 8, rowY, 0);
                                org.lwjgl.opengl.GL11.glScaled(scale, scale, 1);
                                row.icon.drawWhole(0, 0, wrapWidth);
                            } finally {
                                org.lwjgl.opengl.GL11.glPopMatrix();
                            }
                            DialogueFontDrawing.draw(
                                mc.fontRenderer,
                                row.text,
                                left + 8 + 24 * scale,
                                rowY + (20 - mc.fontRenderer.FONT_HEIGHT) / 2 * scale,
                                scale,
                                DgrUiPalette.TEXT);
                        }
                        rowY += rowHeight(row, mc);
                    }
                    if (shown[i] < block.rows.size()) CanonicalDialogueRenderer.drawText(
                        mc.fontRenderer,
                        fit(mc, "! 部分内容已省略", wrapWidth),
                        left + 8,
                        rowY,
                        scale,
                        DgrUiPalette.SECONDARY);
                    taskY += taskHeight + 4;
                }
                y += storyHeight + 4;
                index = end;
            }
        } finally {
            org.lwjgl.opengl.GL11.glPopAttrib();
        }
    }

    private static int taskHeight(Minecraft mc, Block block, int shown, int lineHeight) {
        int height = 8 + lineHeight;
        for (int i = 0; i < shown; i++) {
            height += rowHeight(block.rows.get(i), mc);
        }
        if (shown < block.rows.size()) height += lineHeight;
        return height;
    }

    private static int layoutHeight(Minecraft mc, int[] shown, int count, int lineHeight) {
        int height = 0;
        String story = null;
        for (int i = 0; i < count; i++) {
            Block block = blocks.get(i);
            if (!block.story.equals(story)) {
                if (story != null) height += 4;
                height += 8 + lineHeight;
            }
            height += taskHeight(mc, block, shown[i], lineHeight) + 4;
            story = block.story;
        }
        if (count < blocks.size()) height += (count > 0 ? 4 : 0) + lineHeight + 8;
        return height;
    }

    private static void box(int left, int top, int right, int bottom, int background) {
        Gui.drawRect(left, top, right, bottom, background);
        Gui.drawRect(left, top, right, top + 1, DgrUiPalette.BORDER);
        Gui.drawRect(left, bottom - 1, right, bottom, DgrUiPalette.BORDER);
        Gui.drawRect(left, top, left + 1, bottom, DgrUiPalette.BORDER);
        Gui.drawRect(right - 1, top, right, bottom, DgrUiPalette.BORDER);
    }

    private static void glyph(int x, int y, boolean book, int color) {
        if (book) {
            Gui.drawRect(x, y, x + 3, y + 6, color);
            Gui.drawRect(x + 4, y, x + 7, y + 6, color);
            Gui.drawRect(x + 3, y + 1, x + 4, y + 7, color);
        } else {
            Gui.drawRect(x, y, x + 1, y + 8, color);
            Gui.drawRect(x + 1, y, x + 6, y + 4, color);
        }
    }
}
