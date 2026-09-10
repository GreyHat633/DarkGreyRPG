package darkgrey.rpg.client.gui;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.resources.I18n;

import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

import darkgrey.rpg.client.session.CanonicalDialogueLayout;
import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Separate canonical Session UI; legacy Dialogue screen behavior is unchanged. */
public final class GuiCanonicalSessionScreen extends GuiScreen {

    private static final int PREVIOUS_CHOICES_BUTTON = 2000;
    private static final int NEXT_CHOICES_BUTTON = 2001;
    private static final int CHOICE_BUTTON_BASE = 3000;

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
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
        if (!frame.canContinue()) {
            List<CanonicalSessionChoiceOption> choices = frame.getChoices();
            choiceOffset = Math.min(choiceOffset, Math.max(0, choices.size() - 1));
            int visible = Math.min(layout.choicesPerPage, choices.size() - choiceOffset);
            int firstY = layout.choiceTop(visible);
            int choiceLeft = (width - layout.choiceWidth) / 2;
            for (int index = 0; index < visible; index++) {
                CanonicalSessionChoiceOption option = choices.get(choiceOffset + index);
                int id = CHOICE_BUTTON_BASE + index;
                choiceButtons.put(id, option.getOptionId());
                String label = option.getDisplayText();
                if (fontRendererObj.getStringWidth(label) > layout.choiceWidth - 16)
                    label = fontRendererObj.trimStringToWidth(label, layout.choiceWidth - 28) + "...";
                buttonList.add(new GuiButton(id, choiceLeft, firstY + index * 24, layout.choiceWidth, 20, label));
            }
            int pageY = firstY + visible * 24;
            if (choiceOffset > 0)
                buttonList.add(new GuiButton(PREVIOUS_CHOICES_BUTTON, choiceLeft, pageY, 60, 20, "<"));
            if (choiceOffset + visible < choices.size()) buttonList
                .add(new GuiButton(NEXT_CHOICES_BUTTON, choiceLeft + layout.choiceWidth - 60, pageY, 60, 20, ">"));
        }
        setButtonsEnabled(!awaitingServer);
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (awaitingServer) return;
        if (button.id == PREVIOUS_CHOICES_BUTTON) {
            choiceOffset = Math.max(0, choiceOffset - new CanonicalDialogueLayout(width, height).choicesPerPage);
            initGui();
        } else if (button.id == NEXT_CHOICES_BUTTON) {
            choiceOffset = Math.min(
                frame.getChoices()
                    .size() - 1,
                choiceOffset + new CanonicalDialogueLayout(width, height).choicesPerPage);
            initGui();

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
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (wheel != 0 && new CanonicalDialogueLayout(width, height).containsDialogue(mouseX, mouseY))
            scrollLine = Math.max(0, scrollLine + (wheel < 0 ? 2 : -2));
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
        int left = layout.left;
        int top = layout.top;
        int right = layout.right;
        int bottom = layout.bottom;
        drawRect(left, top, right, bottom, 0xCC161616);
        drawRect(left, top, right, top + 1, 0xFFC0C0C0);
        drawRect(left, bottom - 1, right, bottom, 0xFF888888);
        drawRect(left, top, left + 1, bottom, 0xFF888888);
        drawRect(right - 1, top, right, bottom, 0xFF888888);
        // Reserved empty portrait slot: no authored portrait data in this release.
        drawRect(left + 8, top + 8, left + 8 + layout.portraitSize, top + 8 + layout.portraitSize, 0xFF777777);
        drawRect(left + 9, top + 9, left + 7 + layout.portraitSize, top + 7 + layout.portraitSize, 0xFF202020);
        String speaker = CanonicalSessionClientController.getVisibleSpeaker();
        String text = CanonicalSessionClientController.getVisibleText();
        int textTop = top + 7;
        if (!speaker.isEmpty()) {
            fontRendererObj.drawString(
                fontRendererObj.trimStringToWidth(speaker, layout.textWidth),
                layout.textLeft,
                textTop,
                0xFFE4D5AE);
            textTop += fontRendererObj.FONT_HEIGHT + 3;
        }
        int textBottom = bottom - 15;
        List<String> lines = fontRendererObj.listFormattedStringToWidth(text, layout.textWidth);
        int visibleLines = Math.max(1, (textBottom - textTop) / fontRendererObj.FONT_HEIGHT);
        int maximumScroll = Math.max(0, lines.size() - visibleLines);
        scrollLine = Math.min(scrollLine, maximumScroll);
        for (int index = 0; index < visibleLines && index + scrollLine < lines.size(); index++) {
            fontRendererObj.drawString(
                lines.get(index + scrollLine),
                layout.textLeft,
                textTop + index * fontRendererObj.FONT_HEIGHT,
                0xFFEEEEEE);
        }
        String hint = awaitingServer ? I18n.format("gui.darkgrey_rpg.dialogue.waiting")
            : maximumScroll > 0 ? I18n.format("gui.darkgrey_rpg.dialogue.scroll")
                : frame.canContinue() ? I18n.format("gui.darkgrey_rpg.dialogue.continue") : "";
        fontRendererObj.drawString(
            fontRendererObj.trimStringToWidth(hint, layout.textWidth),
            layout.textLeft,
            bottom - 11,
            0xFFAAAAAA);
        super.drawScreen(mouseX, mouseY, partialTicks);
        for (Object object : buttonList) {
            GuiButton button = (GuiButton) object;
            if (!choiceButtons.containsKey(button.id) || mouseX < button.xPosition
                || mouseX >= button.xPosition + button.width
                || mouseY < button.yPosition
                || mouseY >= button.yPosition + button.height) continue;
            for (CanonicalSessionChoiceOption option : frame.getChoices()) {
                if (option.getOptionId()
                    .equals(choiceButtons.get(button.id))
                    && !option.getDisplayText()
                        .equals(button.displayString))
                    drawHoveringText(
                        fontRendererObj.listFormattedStringToWidth(option.getDisplayText(), Math.max(40, width / 2)),
                        mouseX,
                        mouseY,
                        fontRendererObj);
            }
        }
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        if (button == 0 && !awaitingServer
            && frame.canContinue()
            && new CanonicalDialogueLayout(width, height).containsDialogue(mouseX, mouseY)) sendContinue();
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
