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

    private CanonicalSessionFrame frame;
    private int scrollLine;
    private int choiceOffset;
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
                buttonList.add(new GuiRpgButton(id, choiceLeft, firstY + index * 24, layout.choiceWidth, 20, label));
            }
            int pageY = firstY + visible * 24;
            if (choiceOffset > 0)
                buttonList.add(new GuiRpgButton(PREVIOUS_CHOICES_BUTTON, choiceLeft, pageY, 60, 20, "<"));
            if (choiceOffset + visible < choices.size()) buttonList
                .add(new GuiRpgButton(NEXT_CHOICES_BUTTON, choiceLeft + layout.choiceWidth - 60, pageY, 60, 20, ">"));
        }
        setButtonsEnabled(!awaitingServer);
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (awaitingServer || mc.currentScreen != this) return;
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
        darkgrey.rpg.media.CanonicalSessionScene.draw(width, height);
        scrollLine = CanonicalDialogueRenderer.draw(fontRendererObj, width, height, frame, scrollLine, awaitingServer);
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
            source.getVoiceRef())
                .withPresentation(source.getPresentation(), source.getLineEpoch(), source.shouldPlayVoice());
    }
}
