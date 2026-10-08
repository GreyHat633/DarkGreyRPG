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
    private static final int AUTO_BUTTON = 1998;

    private CanonicalSessionFrame frame;
    private int choiceOffset;
    private int visibleChoiceCount;
    private int focusedChoice = -1;
    private String focusedOption;
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
        boolean same = frame.getKind() == updatedFrame.getKind() && frame.getLineEpoch() == updatedFrame.getLineEpoch()
            && frame.getCurrentNodeId()
                .equals(updatedFrame.getCurrentNodeId());
        frame = copy(updatedFrame);
        if (!same) {
            choiceOffset = 0;
            choicePageHistory.clear();
            focusedOption = null;
        }
        if (same && focusedOption != null) {
            int selected = -1;
            for (int i = 0; i < frame.getChoices()
                .size(); i++)
                if (frame.getChoices()
                    .get(i)
                    .getOptionId()
                    .equals(focusedOption)
                    && frame.getChoices()
                        .get(i)
                        .isEnabled())
                    selected = i;
            if (selected >= 0 && (selected < choiceOffset || selected >= choiceOffset + visibleChoiceCount)) {
                choiceOffset = selected;
                choicePageHistory.clear();
            }
        }
        awaitingServer = CanonicalSessionClientController.presentationModel()
            .awaitingServer();
        if (mc != null) initGui();
    }

    @Override
    public void initGui() {
        buttonList.clear();
        choiceButtons.clear();
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(
            width,
            height,
            DialogueFontDrawing.scale(),
            fontRendererObj.FONT_HEIGHT);
        buttonList.add(new GuiRpgButton(HISTORY_BUTTON, layout.right - 42, layout.buttonTop(), 33, 16, "记录"));
        buttonList.add(new GuiRpgButton(AUTO_BUTTON, layout.right - 78, layout.buttonTop(), 33, 16, "自动"));
        if (!frame.canContinue()) {
            List<CanonicalSessionChoiceOption> choices = frame.getChoices();
            choiceOffset = Math.min(choiceOffset, Math.max(0, choices.size() - 1));
            int regionTop = 4, regionBottom = layout.top - 8;
            int regionHeight = Math.max(1, regionBottom - regionTop);
            int choiceLeft = (width - layout.choiceWidth) / 2;
            int firstY = 0;
            int available = Math.max(26, regionHeight - 24);
            visibleChoiceCount = 0;
            int used = 0;
            for (int index = 0; index < choices.size() - choiceOffset; index++) {
                CanonicalSessionChoiceOption option = choices.get(choiceOffset + index);
                int id = CHOICE_BUTTON_BASE + index;
                String label = option.getDisplayText() + (option.isEnabled() || option.getHint()
                    .isEmpty() ? "" : "\n" + option.getHint());
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
            boolean hasPager = choiceOffset > 0 || choiceOffset + visibleChoiceCount < choices.size();
            int total = Math.max(0, used - 4) + (hasPager ? 24 : 0);
            int centered = regionTop + Math.max(0, (regionHeight - total) / 2);
            for (Object object : buttonList)
                if (object instanceof GuiWrappedChoiceButton) ((GuiButton) object).yPosition += centered;
            pageY += centered;
            if (choiceOffset > 0)
                buttonList.add(new GuiRpgButton(PREVIOUS_CHOICES_BUTTON, choiceLeft, pageY, 60, 20, "<"));
            if (choiceOffset + visibleChoiceCount < choices.size()) buttonList
                .add(new GuiRpgButton(NEXT_CHOICES_BUTTON, choiceLeft + layout.choiceWidth - 60, pageY, 60, 20, ">"));
        }
        for (Object object : buttonList) if (((GuiButton) object).id == AUTO_BUTTON)
            ((GuiRpgButton) object).selected = CanonicalSessionClientController.presentationModel()
                .automatic();
        setButtonsEnabled(!awaitingServer);
        focusedChoice = Math.min(focusedChoice, visibleChoiceCount - 1);
        if (focusedOption != null) {
            focusedChoice = -1;
            for (int i = 0; i < visibleChoiceCount; i++) if (frame.getChoices()
                .get(choiceOffset + i)
                .getOptionId()
                .equals(focusedOption)
                && frame.getChoices()
                    .get(choiceOffset + i)
                    .isEnabled())
                focusedChoice = i;
        }
        updateChoiceFocus();
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (!button.enabled) return;
        if (button.id == AUTO_BUTTON && mc.currentScreen == this) {
            CanonicalSessionClientController.presentationModel()
                .toggleAutomatic();
            try {
                UtilityWindowChrome.settings()
                    .savePlayerPreferences();
            } catch (RuntimeException error) {
                darkgrey.rpg.DarkGreyRpg.LOG.warn("Could not save automatic dialogue preference", error);
            }
            ((GuiRpgButton) button).selected = CanonicalSessionClientController.presentationModel()
                .automatic();
            return;
        }
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
        for (Object object : buttonList) {
            GuiButton button = (GuiButton) object;
            if (!choiceButtons.containsKey(button.id)) continue;
            for (CanonicalSessionChoiceOption option : frame.getChoices()) if (option.getOptionId()
                .equals(choiceButtons.get(button.id))) button.enabled &= option.isEnabled();
        }
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
            int count = frame.getChoices()
                .size();
            int current = focusedChoice < 0 ? (backward ? count : -1) : choiceOffset + focusedChoice;
            int selected = -1;
            for (int step = 1; step <= count; step++) {
                int next = (current + (backward ? -step : step) + count) % count;
                if (frame.getChoices()
                    .get(next)
                    .isEnabled()) {
                    selected = next;
                    break;
                }
            }
            if (selected >= 0 && (selected < choiceOffset || selected >= choiceOffset + visibleChoiceCount)) {
                choiceOffset = selected;
                choicePageHistory.clear();
                focusedOption = frame.getChoices()
                    .get(selected)
                    .getOptionId();
                initGui();
            }
            focusedChoice = selected < 0 ? -1 : selected - choiceOffset;
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
        focusedOption = focusedChoice < 0 || choiceOffset + focusedChoice >= frame.getChoices()
            .size() ? null
                : frame.getChoices()
                    .get(choiceOffset + focusedChoice)
                    .getOptionId();
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
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        darkgrey.rpg.media.CanonicalSessionScene.draw(width, height);
        awaitingServer = CanonicalSessionClientController.presentationModel()
            .awaitingServer();
        double scale = DialogueFontDrawing.scale();
        CanonicalDialogueLayout speakerLayout = new CanonicalDialogueLayout(
            width,
            height,
            scale,
            fontRendererObj.FONT_HEIGHT);
        for (Object object : buttonList) {
            GuiButton button = (GuiButton) object;
            if (button.id == HISTORY_BUTTON || button.id == AUTO_BUTTON) button.yPosition = speakerLayout.buttonTop();
        }
        CanonicalDialogueRenderer.draw(fontRendererObj, width, height, frame, awaitingServer);
        super.drawScreen(mouseX, mouseY, partialTicks);
        if (frame.getKind() == CanonicalSessionFrame.Kind.CHOICE) {
            boolean any = false;
            for (CanonicalSessionChoiceOption option : frame.getChoices()) any |= option.isEnabled();
            if (!any) drawCenteredString(fontRendererObj, "当前没有可选项", width / 2, 6, DgrUiPalette.SECONDARY);
        }
        if (mouseX >= speakerLayout.speakerLeft(CanonicalSessionClientController.getVisiblePortraitRef() != null)
            && mouseX < speakerLayout.speakerLeft(CanonicalSessionClientController.getVisiblePortraitRef() != null)
                + speakerLayout.speakerWidth(CanonicalSessionClientController.getVisiblePortraitRef() != null)
            && mouseY >= speakerLayout.top + 4
            && mouseY < speakerLayout.dividerTop()
            && fontRendererObj.getStringWidth("§l" + CanonicalSessionClientController.getVisibleSpeaker()) * scale
                > speakerLayout.speakerWidth(CanonicalSessionClientController.getVisiblePortraitRef() != null))
            drawHoveringText(
                fontRendererObj.listFormattedStringToWidth(
                    CanonicalSessionClientController.getVisibleSpeaker(),
                    Math.max(40, width / 2)),
                mouseX,
                mouseY,
                fontRendererObj);
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
        CanonicalSessionClientController.presentationModel()
            .pauseAutomatic();
        CanonicalDialogueRenderer.draw(fontRendererObj, width, height, frame, awaitingServer);
        for (Object object : buttonList) ((GuiButton) object).drawButton(mc, -10000, -10000);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        if (mc.currentScreen != this) return;
        if (button == 0) for (Object object : buttonList) {
            GuiButton control = (GuiButton) object;
            if (control.visible && mouseX >= control.xPosition
                && mouseX < control.xPosition + control.width
                && mouseY >= control.yPosition
                && mouseY < control.yPosition + control.height) {
                super.mouseClicked(mouseX, mouseY, button);
                return;
            }
        }
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
                .withPresentation(source.getPresentation(), source.getLineEpoch(), source.shouldPlayVoice())
                .withScreenPlayback(source.shouldPlayScreen())
                .withProjectionRevision(source.getProjectionRevision());
    }
}
