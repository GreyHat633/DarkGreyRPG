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
    private final java.util.Map<Integer, ItemSlotStrip> strips = new java.util.HashMap<Integer, ItemSlotStrip>();

    private int rowHeight() {
        int result = 80;
        for (int i = 0; i < frame.getOptions()
            .size(); i++) {
            int progressHeight = fontRendererObj.listFormattedStringToWidth(progress(i), contentWidth())
                .size() * fontRendererObj.FONT_HEIGHT;
            result = Math.max(
                result,
                18 + strips.get(i)
                    .height(contentWidth()) + 6 + progressHeight + 12);
        }
        return result;
    }

    private int contentWidth() {
        return Math.max(24, Math.min(520, width - 30) - 140);
    }

    private String progress(int index) {
        net.minecraft.nbt.NBTTagCompound preview = frame.getOptions()
            .get(index)
            .getPreview();
        return (preview.getBoolean("group") ? "以下任意物品，合计需要 " + preview.getInteger("required") + " 个 · " : "") + "持有 "
            + preview.getInteger("held")
            + " / 需要 "
            + preview.getInteger("required");
    }

    private int pageSize() {
        return Math.max(1, (Math.min(430, height - 30) - 80) / rowHeight());
    }

    private int panelHeight() {
        return Math.min(
            height - 30,
            80 + Math.min(
                frame.getOptions()
                    .size() - firstOption,
                pageSize()) * rowHeight());
    }

    public GuiCanonicalTaskSubmitChooser(CanonicalTaskSubmitChoiceFrame frame) {
        if (frame == null) throw new IllegalArgumentException("Task submit chooser frame is required.");
        this.frame = frame;
        for (int i = 0; i < frame.getOptions()
            .size(); i++) {
            net.minecraft.nbt.NBTTagCompound preview = frame.getOptions()
                .get(i)
                .getPreview();
            if (!preview.hasKey("items")) {
                net.minecraft.nbt.NBTTagList items = new net.minecraft.nbt.NBTTagList();
                net.minecraft.nbt.NBTTagCompound missing = new net.minecraft.nbt.NBTTagCompound();
                missing.setString("error", "此候选没有物品展示数据");
                items.appendTag(missing);
                preview.setTag("items", items);
            }
            strips.put(i, new ItemSlotStrip(preview, true));
        }
    }

    @Override
    public void initGui() {
        buttonList.clear();
        List<CanonicalTaskSubmitChoiceFrame.Option> options = frame.getOptions();
        int panelWidth = Math.min(520, width - 30);
        int left = (width - panelWidth) / 2;
        int top = (height - panelHeight()) / 2;
        int rowHeight = rowHeight();
        firstOption = Math.max(0, Math.min(firstOption, ((options.size() - 1) / pageSize()) * pageSize()));
        int visible = Math.min(options.size() - firstOption, pageSize());
        for (int row = 0; row < visible; row++) buttonList.add(
            new GuiButton(
                OPTION_BASE + firstOption + row,
                left + panelWidth - 112,
                top + 60 + row * rowHeight,
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
        if (keyCode == Keyboard.KEY_ESCAPE && ItemCandidatePopover.escape()) return;
        if (keyCode == Keyboard.KEY_ESCAPE) submit(-1);
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        ItemCandidatePopover.begin();
        drawDefaultBackground();
        int panelWidth = Math.min(520, width - 30);
        int panelHeight = panelHeight();
        int left = (width - panelWidth) / 2;
        int top = (height - panelHeight) / 2;
        drawRect(left, top, left + panelWidth, top + panelHeight, DgrUiPalette.WINDOW_PANEL);
        darkgrey.rpg.client.gui.DgrUiText
            .centered(fontRendererObj, "选择要提交的任务目标", width / 2, top + 12, DgrUiPalette.TEXT);
        List<CanonicalTaskSubmitChoiceFrame.Option> options = frame.getOptions();
        int visible = Math.min(options.size() - firstOption, pageSize());
        List<String> tooltip = null;
        for (int row = 0; row < visible; row++) {
            int y = top + 38 + row * rowHeight();
            fontRendererObj.drawString(
                fontRendererObj.trimStringToWidth(
                    options.get(firstOption + row)
                        .getDisplay(),
                    panelWidth - 140),
                left + 20,
                y,
                DgrUiPalette.TEXT);
            ItemSlotStrip strip = strips.get(firstOption + row);
            int stripHeight = strip.height(contentWidth());
            strip.draw(left + 20, y + 18, contentWidth(), y + 18, y + 18 + stripHeight, mouseX, mouseY);
            if (strip.tooltip != null) tooltip = strip.tooltip;
            fontRendererObj.drawSplitString(
                progress(firstOption + row),
                left + 20,
                y + 24 + stripHeight,
                contentWidth(),
                DgrUiPalette.SECONDARY);
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
        List<String> popupTooltip = ItemCandidatePopover.draw(width, height, mouseX, mouseY);
        if (popupTooltip != null) drawHoveringText(popupTooltip, mouseX, mouseY, fontRendererObj);
        else if (tooltip != null && !ItemCandidatePopover.contains(mouseX, mouseY))
            drawHoveringText(tooltip, mouseX, mouseY, fontRendererObj);
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (ItemCandidatePopover.click(x, y, button)) return;
        if (button == 0) for (int i = firstOption; i < Math.min(
            frame.getOptions()
                .size(),
            firstOption + pageSize()); i++) {
                ItemSlotStrip strip = strips.get(i);
                if (strip.moreAt(x, y)) {
                    mc.displayGuiScreen(new GuiItemCandidates(this, strip.source()));
                    return;
                }
            }
        super.mouseClicked(x, y, button);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    @Override
    public void handleMouseInput() {
        int x = org.lwjgl.input.Mouse.getEventX() * width / mc.displayWidth;
        int y = height - org.lwjgl.input.Mouse.getEventY() * height / mc.displayHeight - 1;
        if (ItemCandidatePopover.wheel(x, y, org.lwjgl.input.Mouse.getEventDWheel())) return;
        super.handleMouseInput();
    }

    @Override
    public void onGuiClosed() {
        ItemCandidatePopover.close();
        super.onGuiClosed();
    }

    private void submit(int index) {
        submitted = true;
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalTaskSubmitChoiceSelection(frame.getToken(), index));
        mc.displayGuiScreen(null);
    }
}
