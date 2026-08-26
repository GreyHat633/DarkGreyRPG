package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.C2SDialogueAction;
import darkgrey.rpg.network.message.S2CDialogueFrame;

public final class GuiDialogueScreen extends GuiScreen {

    private static final int CONTINUE_BUTTON = 1000;
    private static final int PREVIOUS_CHOICES_BUTTON = 2000;
    private static final int NEXT_CHOICES_BUTTON = 2001;
    private static final int CHOICES_PER_PAGE = 5;

    private S2CDialogueFrame frame;
    private int scrollLine;
    private int choiceOffset;
    private boolean awaitingServer;

    public GuiDialogueScreen(S2CDialogueFrame frame) {
        this.frame = frame;
    }

    public long getSessionId() {
        return frame.getSessionId();
    }

    public void setFrame(S2CDialogueFrame updatedFrame) {
        frame = updatedFrame;
        scrollLine = 0;
        choiceOffset = 0;
        awaitingServer = false;
        if (mc != null) {
            initGui();
        }
    }

    @Override
    public void initGui() {
        buttonList.clear();
        int panelWidth = Math.min(620, width - 40);
        int panelLeft = (width - panelWidth) / 2;
        int panelBottom = Math.min(height - 20, (height + Math.min(420, height - 40)) / 2);

        if (frame.canContinue()) {
            buttonList.add(
                new GuiModernButton(
                    CONTINUE_BUTTON,
                    panelLeft + panelWidth - 130,
                    panelBottom - 34,
                    110,
                    20,
                    "Continue"));
            return;
        }

        List<String> choices = frame.getChoices();
        int visible = Math.min(CHOICES_PER_PAGE, choices.size() - choiceOffset);
        int firstY = panelBottom - 34 - visible * 24;
        for (int index = 0; index < visible; index++) {
            int actualIndex = choiceOffset + index;
            String prefix = actualIndex < 9 ? (actualIndex + 1) + ". " : "";
            buttonList.add(
                new GuiModernButton(
                    index,
                    panelLeft + 20,
                    firstY + index * 24,
                    panelWidth - 40,
                    20,
                    prefix + choices.get(actualIndex)));
        }
        if (choiceOffset > 0) {
            buttonList.add(
                new GuiModernButton(PREVIOUS_CHOICES_BUTTON, panelLeft + 20, panelBottom - 34, 80, 20, "< Previous"));
        }
        if (choiceOffset + visible < choices.size()) {
            buttonList.add(
                new GuiModernButton(
                    NEXT_CHOICES_BUTTON,
                    panelLeft + panelWidth - 100,
                    panelBottom - 34,
                    80,
                    20,
                    "Next >"));
        }
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (awaitingServer) {
            return;
        }
        if (button.id == PREVIOUS_CHOICES_BUTTON) {
            choiceOffset = Math.max(0, choiceOffset - CHOICES_PER_PAGE);
            initGui();
            return;
        }
        if (button.id == NEXT_CHOICES_BUTTON) {
            choiceOffset = Math.min(
                frame.getChoices()
                    .size() - 1,
                choiceOffset + CHOICES_PER_PAGE);
            initGui();
            return;
        }
        if (button.id == CONTINUE_BUTTON) {
            sendAction(DialogueSessionManager.CONTINUE_ACTION);
        } else if (button.id >= 0 && button.id < CHOICES_PER_PAGE) {
            sendAction(choiceOffset + button.id);
        }
    }

    private void sendAction(int choiceIndex) {
        awaitingServer = true;
        for (Object object : buttonList) {
            ((GuiButton) object).enabled = false;
        }
        DialogueNetwork.CHANNEL
            .sendToServer(new C2SDialogueAction(frame.getSessionId(), frame.getNodeId(), choiceIndex));
    }

    @Override
    protected void keyTyped(char typedCharacter, int keyCode) {
        if (keyCode == Keyboard.KEY_ESCAPE) {
            return;
        }
        if (frame.canContinue() && (keyCode == Keyboard.KEY_RETURN || keyCode == Keyboard.KEY_SPACE)) {
            sendAction(DialogueSessionManager.CONTINUE_ACTION);
            return;
        }
        if (!frame.canContinue() && keyCode >= Keyboard.KEY_1 && keyCode <= Keyboard.KEY_9) {
            int choice = keyCode - Keyboard.KEY_1;
            if (choice < frame.getChoices()
                .size()) {
                sendAction(choice);
            }
        }
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel != 0) {
            scrollLine = Math.max(0, scrollLine + (wheel < 0 ? 2 : -2));
        }
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        int panelWidth = Math.min(620, width - 40);
        int panelHeight = Math.min(420, height - 40);
        int left = (width - panelWidth) / 2;
        int top = (height - panelHeight) / 2;
        int right = left + panelWidth;
        int bottom = top + panelHeight;

        drawRect(left, top, right, bottom, 0xF02B2F4A); // Deep Space bg
        drawRect(left, top, right, top + 2, 0xFF7D8CFF); // Deep Space Accent
        drawCenteredString(fontRendererObj, frame.getDialogueId(), width / 2, top + 12, 0xAAEEF0FF); // Secondary text

        int textTop = top + 38;
        if (!frame.getSpeakerName()
            .isEmpty()) {
            fontRendererObj.drawString(frame.getSpeakerName(), left + 24, textTop, 0xFF7D8CFF); // Deep Space Accent
                                                                                                // Name
            textTop += 18;
        }

        int choicesHeight = frame.canContinue() ? 50
            : Math.min(
                CHOICES_PER_PAGE,
                frame.getChoices()
                    .size())
                * 24 + 54;
        int textBottom = bottom - choicesHeight;
        List<String> lines = fontRendererObj.listFormattedStringToWidth(frame.getText(), panelWidth - 48);
        int visibleLines = Math.max(1, (textBottom - textTop) / fontRendererObj.FONT_HEIGHT);
        int maximumScroll = Math.max(0, lines.size() - visibleLines);
        scrollLine = Math.min(scrollLine, maximumScroll);
        for (int index = 0; index < visibleLines && index + scrollLine < lines.size(); index++) {
            fontRendererObj.drawString(
                lines.get(index + scrollLine),
                left + 24,
                textTop + index * fontRendererObj.FONT_HEIGHT,
                0xFFEEF0FF); // Light text
        }
        if (maximumScroll > 0) {
            fontRendererObj.drawString("Mouse wheel to scroll", right - 125, textBottom - 12, 0xAAEEF0FF);
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
