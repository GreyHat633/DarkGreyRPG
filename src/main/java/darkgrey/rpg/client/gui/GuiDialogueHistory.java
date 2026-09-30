package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

import darkgrey.rpg.client.session.DialogueBacklog;
import darkgrey.rpg.client.session.DialogueHistoryClient;
import darkgrey.rpg.client.session.PlayerReadingContext;

/** Read-only reading overlay; close restores the controller's existing foreground. */
public final class GuiDialogueHistory extends GuiScreen {

    private int scroll;
    private int maximumScroll;
    private final java.util.Set<Integer> speakerLines = new java.util.HashSet<Integer>();
    private final List<String> lines = new ArrayList<String>();
    private long layoutRevision = -1;
    private String layoutContext;
    private int layoutWidth;
    private double layoutScale;
    private long fontRevision = -1;
    private boolean unicode;
    private boolean firstLayout = true;

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        int left = Math.max(8, width / 10), right = width - left;
        drawRect(left, 16, right, height - 16, DgrUiPalette.WINDOW_PANEL);
        fontRendererObj.drawString("对话记录", left + 12, 26, DgrUiPalette.TEXT);
        double scale = DialogueFontDrawing.scale();
        int lineHeight = (int) Math.ceil(fontRendererObj.FONT_HEIGHT * scale) + 3;
        String context = PlayerReadingContext.current();
        long revision = DialogueHistoryClient.HISTORY.revision();
        int wrapWidth = Math.max(1, (int) ((right - left - 36) / scale));
        if (revision != layoutRevision || !java.util.Objects.equals(context, layoutContext)
            || wrapWidth != layoutWidth
            || scale != layoutScale
            || fontRevision != darkgrey.rpg.client.ClientResourceRevision.current()
            || unicode != fontRendererObj.getUnicodeFlag()) {
            lines.clear();
            speakerLines.clear();
            for (DialogueBacklog.Entry entry : DialogueHistoryClient.HISTORY.entries(context)) {
                if (!entry.speaker.isEmpty()) {
                    for (String name : (List<String>) fontRendererObj
                        .listFormattedStringToWidth(entry.speaker, wrapWidth)) {
                        speakerLines.add(lines.size());
                        lines.add(name);
                    }
                }
                String text = entry.speaker.isEmpty() ? entry.text : "「" + entry.text + "」";
                lines.addAll(fontRendererObj.listFormattedStringToWidth(text, wrapWidth - 8));
                lines.add("");
            }
            if (lines.isEmpty()) lines.add("暂无对话记录");
            layoutRevision = revision;
            layoutContext = context;
            layoutWidth = wrapWidth;
            layoutScale = scale;
            fontRevision = darkgrey.rpg.client.ClientResourceRevision.current();
            unicode = fontRendererObj.getUnicodeFlag();
        }
        int count = Math.max(1, (height - 82) / lineHeight);
        maximumScroll = Math.max(0, lines.size() - count);
        if (firstLayout) {
            scroll = maximumScroll;
            firstLayout = false;
        }
        scroll = Math.min(scroll, maximumScroll);
        for (int i = 0; i < count && i + scroll < lines.size(); i++) CanonicalDialogueRenderer.drawText(
            fontRendererObj,
            lines.get(i + scroll),
            left + (speakerLines.contains(i + scroll) ? 12 : 20),
            44 + i * lineHeight,
            scale,
            speakerLines.contains(i + scroll) ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.TEXT);
        fontRendererObj.drawString("滚轮阅读 · ESC 返回", left + 12, height - 30, DgrUiPalette.SECONDARY);
        if (maximumScroll > 0) {
            int rail = height - 88, thumb = Math.max(6, rail * count / lines.size());
            int top = 44 + (rail - thumb) * scroll / maximumScroll;
            drawRect(right - 8, top, right - 5, top + thumb, DgrUiPalette.BORDER);
        }
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel != 0) scroll = Math.max(0, Math.min(maximumScroll, scroll + (wheel < 0 ? 3 : -3)));
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key == Keyboard.KEY_ESCAPE
            || (key != 0 && key == darkgrey.rpg.client.ClientQuestKeyHandler.historyKeyCode()))
            mc.displayGuiScreen(null);
        else if (key == Keyboard.KEY_UP || key == Keyboard.KEY_PRIOR)
            scroll = Math.max(0, scroll - (key == Keyboard.KEY_UP ? 1 : 10));
        else if (key == Keyboard.KEY_DOWN || key == Keyboard.KEY_NEXT)
            scroll = Math.min(maximumScroll, scroll + (key == Keyboard.KEY_DOWN ? 1 : 10));
        else if (key == Keyboard.KEY_HOME) scroll = 0;
        else if (key == Keyboard.KEY_END) scroll = maximumScroll;
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
