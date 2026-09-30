package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.client.gui.CanonicalDialogueRenderer;
import darkgrey.rpg.client.gui.DgrUiPalette;
import darkgrey.rpg.client.gui.TaskObjectiveText;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Cached objective summary. Overflow remains fully accessible in task details. */
public final class TaskTrackerHud {

    private static final java.util.Map<Integer, darkgrey.rpg.client.gui.ItemSlotStrip> itemRows = new java.util.HashMap<Integer, darkgrey.rpg.client.gui.ItemSlotStrip>();
    private static final List<String> lines = new ArrayList<String>();
    private static long snapshotRevision = Long.MIN_VALUE, selectionRevision = Long.MIN_VALUE;
    private static int wrapWidth, maximumScroll, left, top, right, bottom;
    private static double scale;
    private static int naturalWidth;
    private static long resourceRevision = -1;

    private TaskTrackerHud() {}

    public static void draw() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null) return;
        ScaledResolution resolution = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight);
        int width = resolution.getScaledWidth(), height = resolution.getScaledHeight();
        double nextScale = PlayerUiPreferences.textScale();
        right = width - 8;
        // Measure the sidebar before wrapping; shifting an already-wide panel is insufficient.
        net.minecraft.scoreboard.ScoreObjective scoreboard = mc.theWorld.getScoreboard()
            .func_96539_a(1);
        if (scoreboard != null) {
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
        // A sidebar that consumes the screen leaves details accessible through the task menu.
        int availableWidth = right - 8;
        if (availableWidth < 30) return;
        int panelWidth = Math.min(200, availableWidth);
        int nextWrap = Math.max(1, (int) ((panelWidth - 20) / nextScale));
        if (snapshotRevision != CanonicalTaskClientStore.getRevision()
            || selectionRevision != TaskTrackerClient.revision()
            || wrapWidth != nextWrap
            || scale != nextScale
            || resourceRevision != ClientResourceRevision.current()) {
            resourceRevision = ClientResourceRevision.current();
            snapshotRevision = CanonicalTaskClientStore.getRevision();
            selectionRevision = TaskTrackerClient.revision();
            wrapWidth = nextWrap;
            scale = nextScale;
            lines.clear();
            itemRows.clear();
            NBTTagList tasks = CanonicalTaskClientStore.getSnapshot()
                .getTagList("tasks", 10);
            for (int i = 0; i < tasks.tagCount(); i++) {
                NBTTagCompound task = tasks.getCompoundTagAt(i);
                if (!TaskTrackerClient.selected()
                    .contains(TaskTrackerClient.identity(task))) continue;
                if (!lines.isEmpty()) lines.add("");
                lines.addAll(mc.fontRenderer.listFormattedStringToWidth(task.getString("title"), wrapWidth));
                NBTTagList objectives = task.getTagList("objectives", 10);
                for (int j = 0; j < objectives.tagCount(); j++) {
                    NBTTagCompound objective = objectives.getCompoundTagAt(j);
                    if (objective.hasKey("item_preview", 10)) {
                        NBTTagCompound preview = (NBTTagCompound) objective.getCompoundTag("item_preview")
                            .copy();
                        NBTTagList original = preview.getTagList("items", 10), compact = new NBTTagList();
                        for (int k = 0; k < Math.min(4, original.tagCount()); k++) compact.appendTag(
                            original.getCompoundTagAt(k)
                                .copy());
                        preview.setTag("items", compact);
                        preview.removeTag("notice");
                        itemRows.put(lines.size(), new darkgrey.rpg.client.gui.ItemSlotStrip(preview, false));
                        lines.add("");
                    }
                    for (String text : TaskObjectiveText.lines(objective)) {
                        // Wrap the body separately so vanilla's space break cannot orphan the bullet.
                        String prefix = text.startsWith("● ") ? "● " : "";
                        List<String> wrapped = mc.fontRenderer.listFormattedStringToWidth(
                            text.substring(prefix.length()),
                            Math.max(1, wrapWidth - mc.fontRenderer.getStringWidth(prefix)));
                        for (int line = 0; line < wrapped.size(); line++)
                            lines.add((line == 0 ? prefix : "") + wrapped.get(line));
                    }
                }
            }
            naturalWidth = itemRows.isEmpty() ? 0 : (int) Math.ceil(Math.min(96, wrapWidth) * scale);
            for (String line : lines)
                naturalWidth = Math.max(naturalWidth, (int) Math.ceil(mc.fontRenderer.getStringWidth(line) * scale));
        }
        if (lines.isEmpty()) return;
        panelWidth = Math.min(panelWidth, naturalWidth + 16);
        int lineHeight = (int) Math.ceil(mc.fontRenderer.FONT_HEIGHT * scale) + 2;
        int totalHeight = 0, visible = 0, limit = Math.max(24, height / 2 - 30);
        for (int i = 0; i < lines.size(); i++) {
            darkgrey.rpg.client.gui.ItemSlotStrip strip = itemRows.get(i);
            int h = strip == null ? lineHeight : (int) Math.ceil(strip.height(wrapWidth) * scale);
            if (totalHeight + h > limit) break;
            totalHeight += h;
            visible++;
        }
        maximumScroll = lines.size() - visible;
        int footer = maximumScroll > 0 ? 14 : 0;
        left = right - panelWidth;
        top = Math.max(4, (height - (totalHeight + 12 + footer)) / 2);
        bottom = top + totalHeight + 12 + footer;
        Gui.drawRect(left, top, right, bottom, DgrUiPalette.HUD_PANEL);
        int y = top + 6;
        for (int i = 0; i < visible; i++) {
            darkgrey.rpg.client.gui.ItemSlotStrip strip = itemRows.get(i);
            if (strip == null) {
                CanonicalDialogueRenderer
                    .drawText(mc.fontRenderer, lines.get(i), left + 7, y, scale, DgrUiPalette.TEXT);
                y += lineHeight;
            } else {
                org.lwjgl.opengl.GL11.glPushMatrix();
                try {
                    org.lwjgl.opengl.GL11.glTranslated(left + 7, y, 0);
                    org.lwjgl.opengl.GL11.glScaled(scale, scale, 1);
                    strip.draw(0, 0, wrapWidth, 0, strip.height(wrapWidth), -100, -100);
                } finally {
                    org.lwjgl.opengl.GL11.glPopMatrix();
                }
                y += (int) Math.ceil(strip.height(wrapWidth) * scale);
            }
        }
        if (maximumScroll > 0) mc.fontRenderer.drawString(
            mc.fontRenderer.trimStringToWidth("更多目标 · 任务菜单中查看", Math.max(1, panelWidth - 16)),
            left + 7,
            bottom - 12,
            DgrUiPalette.SECONDARY);

    }

}
