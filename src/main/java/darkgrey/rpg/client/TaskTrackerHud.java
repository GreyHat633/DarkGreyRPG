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

/** Cached full objective layout. Pointer scrolling is enabled only by the task menu. */
public final class TaskTrackerHud {

    private static final List<String> lines = new ArrayList<String>();
    private static long snapshotRevision = Long.MIN_VALUE, selectionRevision = Long.MIN_VALUE;
    private static int wrapWidth, scroll, maximumScroll, left, top, right, bottom;
    private static double scale;

    private TaskTrackerHud() {}

    public static void draw() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null) return;
        ScaledResolution resolution = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight);
        int width = resolution.getScaledWidth(), height = resolution.getScaledHeight();
        double nextScale = PlayerUiPreferences.textScale();
        int panelWidth = Math.min(200, Math.max(100, width / 3));
        int nextWrap = Math.max(1, (int) ((panelWidth - 20) / nextScale));
        if (snapshotRevision != CanonicalTaskClientStore.getRevision()
            || selectionRevision != TaskTrackerClient.revision()
            || wrapWidth != nextWrap
            || scale != nextScale) {
            snapshotRevision = CanonicalTaskClientStore.getRevision();
            selectionRevision = TaskTrackerClient.revision();
            wrapWidth = nextWrap;
            scale = nextScale;
            lines.clear();
            NBTTagList tasks = CanonicalTaskClientStore.getSnapshot()
                .getTagList("tasks", 10);
            for (int i = 0; i < tasks.tagCount(); i++) {
                NBTTagCompound task = tasks.getCompoundTagAt(i);
                if (!TaskTrackerClient.selected()
                    .contains(TaskTrackerClient.identity(task))) continue;
                if (!lines.isEmpty()) lines.add("");
                lines.addAll(mc.fontRenderer.listFormattedStringToWidth(task.getString("title"), wrapWidth));
                lines.add("────────");
                NBTTagList objectives = task.getTagList("objectives", 10);
                for (int j = 0; j < objectives.tagCount(); j++)
                    for (String text : TaskObjectiveText.lines(objectives.getCompoundTagAt(j)))
                        lines.addAll(mc.fontRenderer.listFormattedStringToWidth(text, wrapWidth));
                if (task.getBoolean("pending_rewards"))
                    lines.addAll(mc.fontRenderer.listFormattedStringToWidth("阶段奖励待发，请腾出背包空间", wrapWidth));
            }
        }
        if (lines.isEmpty()) return;
        int lineHeight = (int) Math.ceil(mc.fontRenderer.FONT_HEIGHT * scale) + 2;
        int visible = Math.max(1, (height / 2 - 30) / lineHeight);
        visible = Math.min(visible, lines.size());
        maximumScroll = Math.max(0, lines.size() - visible);
        scroll = Math.min(scroll, maximumScroll);
        right = width - 8;
        // Vanilla sidebar occupies the same right-middle area. Reserve its measured text width.
        net.minecraft.scoreboard.ScoreObjective scoreboard = mc.theWorld.getScoreboard()
            .func_96539_a(1);
        if (scoreboard != null) {
            int occupied = mc.fontRenderer.getStringWidth(scoreboard.getDisplayName()) + 36;
            for (Object object : mc.theWorld.getScoreboard()
                .func_96534_i(scoreboard)) {
                net.minecraft.scoreboard.Score score = (net.minecraft.scoreboard.Score) object;
                occupied = Math.max(
                    occupied,
                    mc.fontRenderer.getStringWidth(score.getPlayerName() + ": " + score.getScorePoints()) + 36);
            }
            right = Math.max(panelWidth + 8, right - occupied);
        }
        left = right - panelWidth;
        top = Math.max(48, height / 2 - 30);
        bottom = Math.min(height - 25, top + visible * lineHeight + 24);
        visible = Math.max(1, (bottom - top - 24) / lineHeight);
        maximumScroll = Math.max(0, lines.size() - visible);
        scroll = Math.min(scroll, maximumScroll);
        Gui.drawRect(left, top, right, bottom, DgrUiPalette.WINDOW_PANEL);
        for (int i = 0; i < visible && i + scroll < lines.size(); i++) CanonicalDialogueRenderer.drawText(
            mc.fontRenderer,
            lines.get(i + scroll),
            left + 7,
            top + 6 + i * lineHeight,
            scale,
            DgrUiPalette.TEXT);
        if (maximumScroll > 0) {
            mc.fontRenderer.drawString(
                "另有 " + (lines.size() - scroll - visible) + " 行 · 任务菜单中滚动",
                left + 7,
                bottom - 12,
                DgrUiPalette.SECONDARY);
            int rail = bottom - top - 8, thumb = Math.max(4, rail * visible / lines.size());
            int y = top + 4 + (rail - thumb) * scroll / maximumScroll;
            Gui.drawRect(right - 5, y, right - 3, y + thumb, DgrUiPalette.BORDER);
        }
    }

    public static boolean scrollAt(int x, int y, int amount) {
        if (lines.isEmpty() || x < left || x >= right || y < top || y >= bottom) return false;
        scroll = Math.max(0, Math.min(maximumScroll, scroll + amount));
        return true;
    }
}
