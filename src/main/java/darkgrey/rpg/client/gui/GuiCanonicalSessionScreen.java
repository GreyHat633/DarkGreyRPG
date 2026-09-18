package darkgrey.rpg.client.gui;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

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
    private static final int HISTORY_BUTTON = 1999;

    private CanonicalSessionFrame frame;
    private int scrollLine;
    private int choiceOffset;
    private int visibleChoiceCount;
    private int focusedChoice = -1;
    private final java.util.List<Integer> choicePageHistory = new java.util.ArrayList<Integer>();
    private boolean awaitingServer;
    private final Map<Integer, String> choiceButtons = new HashMap<Integer, String>();

    public GuiCanonicalSessionScreen(CanonicalSessionFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Canonical Session frame is required.");
        this.frame = copy(frame);
        allowUserInput = true;
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
        choicePageHistory.clear();
        awaitingServer = false;
        if (mc != null) initGui();
    }

    @Override
    public void initGui() {
        buttonList.clear();
        choiceButtons.clear();
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
        buttonList.add(new GuiRpgButton(HISTORY_BUTTON, layout.right - 48, layout.bottom - 22, 46, 20, "记录"));
        if (!frame.canContinue()) {
            List<CanonicalSessionChoiceOption> choices = frame.getChoices();
            choiceOffset = Math.min(choiceOffset, Math.max(0, choices.size() - 1));
            int firstY = 22;
            int available = Math.max(36, layout.top - 52);
            visibleChoiceCount = 0;
            int choiceLeft = (width - layout.choiceWidth) / 2;
            int used = 0;
            for (int index = 0; index < choices.size() - choiceOffset; index++) {
                CanonicalSessionChoiceOption option = choices.get(choiceOffset + index);
                int id = CHOICE_BUTTON_BASE + index;
                String label = option.getDisplayText();
                GuiWrappedChoiceButton button = new GuiWrappedChoiceButton(
                    id,
                    choiceLeft,
                    firstY + used,
                    layout.choiceWidth,
                    available,
                    label,
                    fontRendererObj);
                if (used > 0 && used + button.height > available) break;
                choiceButtons.put(id, option.getOptionId());
                buttonList.add(button);
                used += button.height + 4;
                visibleChoiceCount++;
            }
            int pageY = firstY + used;
            if (choiceOffset > 0)
                buttonList.add(new GuiRpgButton(PREVIOUS_CHOICES_BUTTON, choiceLeft, pageY, 60, 20, "<"));
            if (choiceOffset + visibleChoiceCount < choices.size()) buttonList
                .add(new GuiRpgButton(NEXT_CHOICES_BUTTON, choiceLeft + layout.choiceWidth - 60, pageY, 60, 20, ">"));
        }
        setButtonsEnabled(!awaitingServer);
        focusedChoice = Math.min(focusedChoice, visibleChoiceCount - 1);
        updateChoiceFocus();
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == HISTORY_BUTTON && mc.currentScreen == this) {
            mc.displayGuiScreen(new GuiDialogueHistory());
            return;
        }
        if (awaitingServer || mc.currentScreen != this) return;
        if (button.id == PREVIOUS_CHOICES_BUTTON) {
            choiceOffset = choicePageHistory.isEmpty() ? 0 : choicePageHistory.remove(choicePageHistory.size() - 1);
            initGui();
        } else if (button.id == NEXT_CHOICES_BUTTON) {
            choicePageHistory.add(choiceOffset);
            choiceOffset = Math.min(
                frame.getChoices()
                    .size() - 1,
                choiceOffset + visibleChoiceCount);
            initGui();

        } else if (choiceButtons.containsKey(button.id)) {
            sendChoice(choiceButtons.get(button.id));
        }
    }

    private void sendContinue() {
        awaitingServer = CanonicalSessionClientController.sendContinue();
        setButtonsEnabled(!awaitingServer);
    }

    private void sendChoice(String optionId) {
        awaitingServer = true;
        setButtonsEnabled(false);
        CanonicalSessionClientController.sendChoice(optionId);
    }

    private void setButtonsEnabled(boolean enabled) {
        for (Object object : buttonList)
            ((GuiButton) object).enabled = ((GuiButton) object).id == HISTORY_BUTTON || enabled;
    }

    /** Minecraft 1.7 normally consumes GUI input before its keybinding loop. */
    @Override
    public void handleInput() {
        // Consume pointer input here so vanilla cannot also attack/use/scroll the hotbar.
        // Leave keyboard events for Minecraft's configured bindings and FML input routing.
        if (Mouse.isCreated()) while (Mouse.next()) {
            if (mc.currentScreen == this) handleMouseInput();
            else if (mc.currentScreen != null) mc.currentScreen.handleMouseInput();
        }
    }

    @Override
    protected void keyTyped(char typedCharacter, int keyCode) {
        if (mc.currentScreen != this) return;
        if (!frame.canContinue() && !awaitingServer && choiceKeyboard(keyCode)) return;
        if (keyCode == Keyboard.KEY_ESCAPE) {
            mc.displayGuiScreen(new net.minecraft.client.gui.GuiIngameMenu());
            if (mc.isSingleplayer() && !mc.getIntegratedServer()
                .getPublic()) mc.getSoundHandler()
                    .pauseSounds();
        } else if (keyCode == mc.gameSettings.keyBindCommand.getKeyCode()
            && mc.gameSettings.chatVisibility != net.minecraft.entity.player.EntityPlayer.EnumChatVisibility.HIDDEN) {
                // Vanilla's command binding requires currentScreen == null; use its configured key.
                mc.displayGuiScreen(new net.minecraft.client.gui.GuiChat("/"));
            }
    }

    private boolean choiceKeyboard(int key) {
        // User-configured DGR bindings take precedence over reading navigation.
        if (key == darkgrey.rpg.client.ClientQuestKeyHandler.settingsKeyCode()
            || key == darkgrey.rpg.client.ClientQuestKeyHandler.historyKeyCode()) return false;
        if (key == Keyboard.KEY_TAB || key == Keyboard.KEY_UP || key == Keyboard.KEY_DOWN) {
            boolean backward = key == Keyboard.KEY_UP || key == Keyboard.KEY_TAB && isShiftKeyDown();
            int next = focusedChoice < 0 ? (backward ? visibleChoiceCount - 1 : 0)
                : focusedChoice + (backward ? -1 : 1);
            if (next >= visibleChoiceCount && choiceOffset + visibleChoiceCount < frame.getChoices()
                .size()) {
                actionPerformed(new GuiButton(NEXT_CHOICES_BUTTON, 0, 0, ""));
                focusedChoice = 0;
            } else if (next < 0 && choiceOffset > 0) {
                actionPerformed(new GuiButton(PREVIOUS_CHOICES_BUTTON, 0, 0, ""));
                focusedChoice = visibleChoiceCount - 1;
            } else focusedChoice = Math.max(0, Math.min(visibleChoiceCount - 1, next));
            updateChoiceFocus();
            return true;
        }
        if (focusedChoice < 0) return false;
        for (Object object : buttonList) if (object instanceof GuiWrappedChoiceButton) {
            GuiWrappedChoiceButton button = (GuiWrappedChoiceButton) object;
            if (button.id != CHOICE_BUTTON_BASE + focusedChoice) continue;
            if (key == Keyboard.KEY_RETURN || key == Keyboard.KEY_NUMPADENTER) actionPerformed(button);
            else if (key == Keyboard.KEY_PRIOR) button.scroll(-5);
            else if (key == Keyboard.KEY_NEXT) button.scroll(5);
            else if (key == Keyboard.KEY_HOME) button.scroll(-Integer.MAX_VALUE / 2);
            else if (key == Keyboard.KEY_END) button.scroll(Integer.MAX_VALUE / 2);
            else return false;
            return true;
        }
        return false;
    }

    private void updateChoiceFocus() {
        for (Object object : buttonList) if (object instanceof GuiWrappedChoiceButton) ((GuiWrappedChoiceButton) object)
            .setKeyboardFocused(((GuiButton) object).id == CHOICE_BUTTON_BASE + focusedChoice);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (wheel != 0) for (Object object : buttonList) {
            if (object instanceof GuiWrappedChoiceButton
                && ((GuiWrappedChoiceButton) object).contains(mouseX, mouseY)) {
                ((GuiWrappedChoiceButton) object).scroll(wheel < 0 ? 2 : -2);
                return;
            }
        }
        if (wheel != 0 && new CanonicalDialogueLayout(width, height).containsDialogue(mouseX, mouseY))
            scrollLine = Math.max(0, scrollLine + (wheel < 0 ? 2 : -2));
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        darkgrey.rpg.media.CanonicalSessionScene.draw(width, height);
        scrollLine = CanonicalDialogueRenderer.draw(fontRendererObj, width, height, frame, scrollLine, awaitingServer);
        if (frame.getKind() == CanonicalSessionFrame.Kind.CHOICE && !frame.getText()
            .isEmpty()) {
            CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
            String prompt = fontRendererObj.trimStringToWidth(frame.getText(), layout.choiceWidth);
            drawCenteredString(fontRendererObj, prompt, width / 2, 4, DgrUiPalette.TEXT);
        }
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

    /** Render-only view used beneath any higher-priority GUI; no hover or input. */
    public void drawUnderlay(float partialTicks) {
        CanonicalDialogueRenderer.draw(fontRendererObj, width, height, frame, scrollLine, awaitingServer);
        for (Object object : buttonList) ((GuiButton) object).drawButton(mc, -10000, -10000);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        if (mc.currentScreen != this) return;
        super.mouseClicked(mouseX, mouseY, button);
        if (mc.currentScreen != this) return;
        if (button == 0 && !awaitingServer && frame.canContinue()) sendContinue();
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
            source.getChoices(),
            source.getPortraitRef(),
            source.getVoiceRef(),
            source.getVoiceVolume()).withTextSpeed(source.getTextSpeed())
                .withPresentation(source.getPresentation(), source.getLineEpoch(), source.shouldPlayVoice());
    }
}
