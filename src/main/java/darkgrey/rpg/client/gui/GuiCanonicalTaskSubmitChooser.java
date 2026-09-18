package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame;
import darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceSelection;

/** Non-pausing chooser for multiple active submit targets on one actor. */
public final class GuiCanonicalTaskSubmitChooser extends GuiScreen {

    private static final int OPTION_BASE = 1000;
    private static final int CANCEL = 2000;
    private static final int PREVIOUS = 2001;
    private static final int NEXT = 2002;
    private final CanonicalTaskSubmitChoiceFrame frame;
    private boolean submitted;
    private int firstOption;

    private int pageSize() {
        return Math.max(1, (Math.min(430, height - 30) - 80) / 42);
    }

    public GuiCanonicalTaskSubmitChooser(CanonicalTaskSubmitChoiceFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Task submit chooser frame is required.");
        this.frame = frame;
    }

    @Override
    public void initGui() {
        buttonList.clear();
        List<CanonicalTaskSubmitChoiceFrame.Option> options = frame.getOptions();
        int panelWidth = Math.min(700, width - 30);
        int left = (width - panelWidth) / 2;
        int top = (height - Math.min(430, height - 30)) / 2;
        int rowHeight = 42;
        firstOption = Math.max(0, Math.min(firstOption, ((options.size() - 1) / pageSize()) * pageSize()));
        int visible = Math.min(options.size() - firstOption, pageSize());
        for (int row = 0; row < visible; row++) buttonList.add(
            new GuiButton(
                OPTION_BASE + firstOption + row,
                left + panelWidth - 112,
                top + 38 + row * rowHeight,
                96,
                20,
                "提交"));
        buttonList.add(new GuiButton(CANCEL, left + panelWidth - 90, top + 38 + visible * rowHeight + 8, 70, 20, "取消"));
        if (options.size() > pageSize()) {
            GuiButton previous = new GuiButton(PREVIOUS, left + 16, top + 38 + visible * rowHeight + 8, 70, 20, "上一页");
            GuiButton next = new GuiButton(NEXT, left + 92, top + 38 + visible * rowHeight + 8, 70, 20, "下一页");
            previous.enabled = firstOption > 0;
            next.enabled = firstOption + visible < options.size();
            buttonList.add(previous);
            buttonList.add(next);
        }
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (submitted) return;
        if (button.id == PREVIOUS || button.id == NEXT) {
            firstOption += button.id == NEXT ? pageSize() : -pageSize();
            initGui();
            return;
        }
        if (button.id == CANCEL) submit(-1);
        else if (button.id >= OPTION_BASE && button.id < OPTION_BASE + frame.getOptions()
            .size()) submit(button.id - OPTION_BASE);
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
        drawRect(left, top, left + panelWidth, top + panelHeight, DgrUiPalette.WINDOW_PANEL);
        drawCenteredString(fontRendererObj, "选择要提交的任务目标", width / 2, top + 12, DgrUiPalette.TEXT);
        List<CanonicalTaskSubmitChoiceFrame.Option> options = frame.getOptions();
        int visible = Math.min(options.size() - firstOption, pageSize());
        for (int row = 0; row < visible; row++) {
            int y = top + 38 + row * 42;
            fontRendererObj.drawString(
                fontRendererObj.trimStringToWidth(
                    options.get(firstOption + row)
                        .getDisplay(),
                    panelWidth - 140),
                left + 20,
                y,
                DgrUiPalette.TEXT);
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    private void submit(int index) {
        submitted = true;
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalTaskSubmitChoiceSelection(frame.getToken(), index));
        mc.displayGuiScreen(null);
    }
}
