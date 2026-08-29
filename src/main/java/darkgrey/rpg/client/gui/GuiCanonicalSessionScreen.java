package darkgrey.rpg.client.gui;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Separate canonical Session UI; legacy Dialogue screen behavior is unchanged. */
public final class GuiCanonicalSessionScreen extends GuiScreen {

    private static final int CONTINUE_BUTTON = 1000;
    private static final int PREVIOUS_CHOICES_BUTTON = 2000;
    private static final int NEXT_CHOICES_BUTTON = 2001;
    private static final int CHOICE_BUTTON_BASE = 3000;
    private static final int CHOICES_PER_PAGE = 5;

    private CanonicalSessionFrame frame;
    private int scrollLine;
    private int choiceOffset;
    private boolean awaitingServer;
    private final Map<Integer, String> choiceButtons = new HashMap<Integer, String>();

    public GuiCanonicalSessionScreen(CanonicalSessionFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Canonical Session frame is required.");
        this.frame = copy(frame);
    }

    public long getTransportId() {
        return frame.getTransportId();
    }

    public long getSessionId() {
        return getTransportId();
    }

    public String getStoryId() {
        return frame.getStoryId();
    }

    public String getSessionResourceId() {
        return frame.getSessionResourceId();
    }

    public boolean matches(long transportId, String storyId) {
        return frame.getTransportId() == transportId && frame.getStoryId()
            .equals(storyId);
    }

    public boolean matches(CanonicalSessionFrame candidate) {
        return candidate != null && matches(candidate.getTransportId(), candidate.getStoryId())
            && frame.getSessionResourceId()
                .equals(candidate.getSessionResourceId());
    }

    public void setFrame(CanonicalSessionFrame updatedFrame) {
        if (!matches(updatedFrame)) return;
        frame = copy(updatedFrame);
        scrollLine = 0;
        choiceOffset = 0;
        awaitingServer = false;
        if (mc != null) initGui();
    }

    @Override
    public void initGui() {
        buttonList.clear();
        choiceButtons.clear();
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
        } else {
            List<CanonicalSessionChoiceOption> choices = frame.getChoices();
            int visible = Math.min(CHOICES_PER_PAGE, choices.size() - choiceOffset);
            int firstY = panelBottom - 34 - visible * 24;
            for (int index = 0; index < visible; index++) {
                int actualIndex = choiceOffset + index;
                int id = CHOICE_BUTTON_BASE + index;
                choiceButtons.put(
                    id,
                    choices.get(actualIndex)
                        .getOptionId());
                buttonList.add(
                    new GuiModernButton(
                        id,
                        panelLeft + 20,
                        firstY + index * 24,
                        panelWidth - 40,
                        20,
                        choices.get(actualIndex)
                            .getDisplayText()));
            }
            if (choiceOffset > 0) buttonList.add(
                new GuiModernButton(PREVIOUS_CHOICES_BUTTON, panelLeft + 20, panelBottom - 34, 80, 20, "< Previous"));
            if (choiceOffset + visible < choices.size()) buttonList.add(
                new GuiModernButton(
                    NEXT_CHOICES_BUTTON,
                    panelLeft + panelWidth - 100,
                    panelBottom - 34,
                    80,
                    20,
                    "Next >"));
        }
        setButtonsEnabled(!awaitingServer);
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (awaitingServer) return;
        if (button.id == PREVIOUS_CHOICES_BUTTON) {
            choiceOffset = Math.max(0, choiceOffset - CHOICES_PER_PAGE);
            initGui();
        } else if (button.id == NEXT_CHOICES_BUTTON) {
            choiceOffset = Math.min(
                frame.getChoices()
                    .size() - 1,
                choiceOffset + CHOICES_PER_PAGE);
            initGui();
        } else if (button.id == CONTINUE_BUTTON) {
            sendContinue();
        } else if (choiceButtons.containsKey(button.id)) {
            sendChoice(choiceButtons.get(button.id));
        }
    }

    private void sendContinue() {
        awaitingServer = true;
        setButtonsEnabled(false);
        CanonicalSessionClientController.sendContinue();
    }

    private void sendChoice(String optionId) {
        awaitingServer = true;
        setButtonsEnabled(false);
        CanonicalSessionClientController.sendChoice(optionId);
    }

    private void setButtonsEnabled(boolean enabled) {
        for (Object object : buttonList) ((GuiButton) object).enabled = enabled;
    }

    @Override
    protected void keyTyped(char typedCharacter, int keyCode) {
        // Escape intentionally does nothing: a server-owned Session cannot be abandoned locally.
        if (keyCode == Keyboard.KEY_ESCAPE) return;
        if (!awaitingServer && frame.canContinue() && (keyCode == Keyboard.KEY_RETURN || keyCode == Keyboard.KEY_SPACE))
            sendContinue();
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel != 0) scrollLine = Math.max(0, scrollLine + (wheel < 0 ? 2 : -2));
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
        drawRect(left, top, right, bottom, 0xF02B2F4A);
        drawRect(left, top, right, top + 2, 0xFF7D8CFF);
        drawCenteredString(fontRendererObj, frame.getStoryId(), width / 2, top + 12, 0xAAEEF0FF);

        int textTop = top + 38;
        if (!frame.getSpeaker()
            .isEmpty()) {
            fontRendererObj.drawString(frame.getSpeaker(), left + 24, textTop, 0xFF7D8CFF);
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
                0xFFEEF0FF);
        }
        if (maximumScroll > 0)
            fontRendererObj.drawString("Mouse wheel to scroll", right - 125, textBottom - 12, 0xAAEEF0FF);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    private static CanonicalSessionFrame copy(CanonicalSessionFrame source) {
        return new CanonicalSessionFrame(
            source.getTransportId(),
            source.getStoryId(),
            source.getSessionResourceId(),
            source.getCurrentNodeId(),
            source.getKind(),
            source.getSpeaker(),
            source.getText(),
            source.getChoices());
    }
}
