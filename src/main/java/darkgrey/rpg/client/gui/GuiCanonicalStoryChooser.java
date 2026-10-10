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
    private static final int ROW_HEIGHT = 38;
    private int panelWidth, panelHeight, left, top;
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
        panelWidth = Math.min(360, width - 24);
        pageSize = Math.max(1, Math.min(6, (Math.min(310, height - 24) - 66) / ROW_HEIGHT));
        page = Math.min(page, Math.max(0, (options.size() - 1) / pageSize));
        int first = page * pageSize;
        int visible = Math.min(pageSize, options.size() - first);
        panelHeight = 66 + visible * ROW_HEIGHT;
        left = (width - panelWidth) / 2;
        top = (height - panelHeight) / 2;
        for (int row = 0; row < visible; row++) {
            int index = first + row;
            buttonList.add(
                new GuiRpgButton(
                    OPTION_BUTTON_BASE + row,
                    left + panelWidth - 82,
                    top + 35 + row * ROW_HEIGHT,
                    70,
                    20,
                    statusText(
                        options.get(index)
                            .getStatus())));
        }
        int navigationY = top + 36 + visible * ROW_HEIGHT;
        if (page > 0) buttonList.add(new GuiRpgButton(PREVIOUS_BUTTON, left + 12, navigationY, 55, 20, "上一页"));
        if (first + visible < options.size())
            buttonList.add(new GuiRpgButton(NEXT_BUTTON, left + 73, navigationY, 55, 20, "下一页"));
        buttonList.add(new GuiRpgButton(CANCEL_BUTTON, left + panelWidth - 82, navigationY, 70, 20, "取消"));
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
        DgrUiPalette.apply();
        drawRect(left, top, left + panelWidth, top + panelHeight, DgrUiPalette.WINDOW_PANEL);
        RuntimeDirectoryVisuals.heading(fontRendererObj, "选择要进行的故事", left + 12, top + 10, panelWidth - 24);
        List<CanonicalStoryChooserFrame.Option> options = frame.getOptions();
        int first = page * pageSize;
        int visible = Math.min(pageSize, options.size() - first);
        for (int row = 0; row < visible; row++) {
            CanonicalStoryChooserFrame.Option option = options.get(first + row);
            int y = top + 35 + row * ROW_HEIGHT;
            DgrUiText.left(
                fontRendererObj,
                "\u00a7l" + fontRendererObj.trimStringToWidth(option.getDisplayName(), panelWidth - 110),
                left + 12,
                y,
                DgrUiPalette.TEXT);
            DgrUiText.left(
                fontRendererObj,
                fontRendererObj.trimStringToWidth(option.getGroupName(), panelWidth - 110),
                left + 12,
                y + 14,
                DgrUiPalette.SECONDARY);
            drawRect(left + 12, y + 31, left + panelWidth - 12, y + 32, DgrUiPalette.BORDER);
        }
        if (options.size() > pageSize) DgrUiText.centered(
            fontRendererObj,
            (page + 1) + " / " + ((options.size() - 1) / pageSize + 1),
            width / 2,
            top + panelHeight - 22,
            DgrUiPalette.SECONDARY);
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
