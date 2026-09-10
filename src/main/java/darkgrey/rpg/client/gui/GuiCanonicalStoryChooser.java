package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalStoryChooserFrame;
import darkgrey.rpg.network.message.canonical.CanonicalStoryChooserSelection;

/** Small non-pausing chooser for server-issued canonical Story actor candidates. */
public final class GuiCanonicalStoryChooser extends GuiScreen {

    private int pageSize = 1;
    private int rowHeight = 52;
    private static final int OPTION_BUTTON_BASE = 1000;
    private static final int PREVIOUS_BUTTON = 2000;
    private static final int NEXT_BUTTON = 2001;
    private static final int CANCEL_BUTTON = 2002;

    private final CanonicalStoryChooserFrame frame;
    private int page;
    private boolean submitted;

    public GuiCanonicalStoryChooser(CanonicalStoryChooserFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Canonical Story chooser frame is required.");
        this.frame = frame;
    }

    @Override
    public void initGui() {
        buttonList.clear();
        List<CanonicalStoryChooserFrame.Option> options = frame.getOptions();
        int panelWidth = Math.min(700, width - 30);
        int left = (width - panelWidth) / 2;
        int panelTop = (height - Math.min(430, height - 30)) / 2;
        rowHeight = 52;
        for (CanonicalStoryChooserFrame.Option option : options) rowHeight = Math.max(
            rowHeight,
            34 + fontRendererObj.FONT_HEIGHT * fontRendererObj
                .listFormattedStringToWidth("ID: " + option.getStoryId(), Math.max(40, panelWidth - 155))
                .size());
        pageSize = Math.max(1, Math.min(6, (Math.min(430, height - 30) - 100) / rowHeight));
        page = Math.min(page, Math.max(0, (options.size() - 1) / pageSize));
        int first = page * pageSize;
        int visible = Math.min(pageSize, options.size() - first);
        for (int row = 0; row < visible; row++) {
            int index = first + row;
            buttonList.add(
                new GuiButton(
                    OPTION_BUTTON_BASE + row,
                    left + panelWidth - 112,
                    panelTop + 40 + row * rowHeight,
                    96,
                    20,
                    statusText(
                        options.get(index)
                            .getStatus())));
        }
        int navigationY = panelTop + 40 + visible * rowHeight + 8;
        if (page > 0) buttonList.add(new GuiButton(PREVIOUS_BUTTON, left + 20, navigationY, 80, 20, "上一页"));
        if (first + visible < options.size())
            buttonList.add(new GuiButton(NEXT_BUTTON, left + panelWidth - 180, navigationY, 80, 20, "下一页"));
        buttonList.add(new GuiButton(CANCEL_BUTTON, left + panelWidth - 90, navigationY, 70, 20, "取消"));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (submitted) return;
        if (button.id == PREVIOUS_BUTTON) {
            page = Math.max(0, page - 1);
            initGui();
        } else if (button.id == NEXT_BUTTON) {
            page++;
            initGui();
        } else if (button.id == CANCEL_BUTTON) {
            submit(-1);
        } else if (button.id >= OPTION_BUTTON_BASE && button.id < OPTION_BUTTON_BASE + pageSize) {
            int index = page * pageSize + button.id - OPTION_BUTTON_BASE;
            if (index < frame.getOptions()
                .size()) submit(index);
        }
    }

    @Override
    protected void keyTyped(char typedCharacter, int keyCode) {
        if (keyCode == Keyboard.KEY_ESCAPE) submit(-1);
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        int panelWidth = Math.min(700, width - 30);
        int panelHeight = Math.min(430, height - 30);
        int left = (width - panelWidth) / 2;
        int top = (height - panelHeight) / 2;
        drawRect(left, top, left + panelWidth, top + panelHeight, 0xEE303030);
        drawCenteredString(fontRendererObj, "有多个故事可供选择", width / 2, top + 12, 0xFFFFFF);
        List<CanonicalStoryChooserFrame.Option> options = frame.getOptions();
        int first = page * pageSize;
        int visible = Math.min(pageSize, options.size() - first);
        for (int row = 0; row < visible; row++) {
            CanonicalStoryChooserFrame.Option option = options.get(first + row);
            int y = top + 40 + row * rowHeight;
            fontRendererObj.drawString(
                fontRendererObj.trimStringToWidth(option.getDisplayName(), panelWidth - 155),
                left + 20,
                y,
                0xFFFFFF);
            fontRendererObj.drawString(
                option.getStatus()
                    .toUpperCase(java.util.Locale.ROOT),
                left + 20,
                y + 12,
                DgrUiPalette.SECONDARY);
            List<String> idLines = fontRendererObj
                .listFormattedStringToWidth("ID: " + option.getStoryId(), Math.max(40, panelWidth - 155));
            for (int line = 0; line < idLines.size(); line++) fontRendererObj
                .drawString(idLines.get(line), left + 20, y + 24 + line * fontRendererObj.FONT_HEIGHT, 0xFFAAAAAA);
        }
        drawCenteredString(fontRendererObj, "第 " + (page + 1) + " 页", width / 2, top + panelHeight - 18, 0xFFAAAAAA);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    private void submit(int index) {
        submitted = true;
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalStoryChooserSelection(frame.getToken(), index));
        mc.displayGuiScreen(null);
    }

    private static String statusText(String status) {
        if ("continue".equals(status)) return "继续";
        if ("restart".equals(status)) return "重新开始";
        return "开始";
    }
}
