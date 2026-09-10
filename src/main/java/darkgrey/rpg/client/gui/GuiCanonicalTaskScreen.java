package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiScreen;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.input.Mouse;

import darkgrey.rpg.creator.CanonicalTaskUiRequest;
import darkgrey.rpg.network.DialogueNetwork;

/** Canonical Task presentation only, including every simultaneously active objective. */
public final class GuiCanonicalTaskScreen extends GuiScreen {

    private static int nextRequest;
    private final int request = ++nextRequest;
    private NBTTagCompound snapshot;
    private int ticks, scroll;

    @Override
    public void initGui() {
        request();
    }

    private void request() {
        DialogueNetwork.CHANNEL.sendToServer(new CanonicalTaskUiRequest(request));
    }

    @Override
    public void updateScreen() {
        if (++ticks % 20 == 0) request();
    }

    public void accept(NBTTagCompound data) {
        if (data.getInteger("request") == request) snapshot = (NBTTagCompound) data.copy();
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel != 0) scroll = Math.max(0, scroll + (wheel < 0 ? 3 : -3));
    }

    @Override
    public void drawScreen(int mx, int my, float partial) {
        drawDefaultBackground();
        int w = Math.min(520, width - 24), left = (width - w) / 2;
        drawRect(left, 12, left + w, height - 12, 0xEE303030);
        drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.tasks"),
            width / 2,
            22,
            0xFFFFFF);
        List<String> lines = new ArrayList<String>();
        if (snapshot == null) lines.add(net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.loading"));
        else {
            NBTTagList tasks = snapshot.getTagList("tasks", 10);
            if (tasks.tagCount() == 0)
                lines.add(net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.no_tasks"));
            for (int i = 0; i < tasks.tagCount(); i++) {
                NBTTagCompound task = tasks.getCompoundTagAt(i);
                lines.addAll(fontRendererObj.listFormattedStringToWidth("\u00a7e" + task.getString("title"), w - 24));
                NBTTagList objectives = task.getTagList("objectives", 10);
                for (int j = 0; j < objectives.tagCount(); j++) {
                    NBTTagCompound objective = objectives.getCompoundTagAt(j);
                    lines
                        .addAll(fontRendererObj.listFormattedStringToWidth("- " + objective.getString("text"), w - 24));
                    if (objective.getInteger("required") > 1)
                        lines.add("  " + objective.getInteger("current") + " / " + objective.getInteger("required"));
                }
                if (objectives.tagCount() == 0) lines.add(
                    task.getBoolean("complete")
                        ? net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.objective_complete")
                        : net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.waiting_objective"));
                lines.add("");
            }
        }
        int visible = Math.max(1, (height - 76) / 12);
        scroll = Math.min(scroll, Math.max(0, lines.size() - visible));
        for (int i = 0; i < visible && i + scroll < lines.size(); i++)
            fontRendererObj.drawString(lines.get(i + scroll), left + 12, 42 + i * 12, 0xDDDDDD);
        drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.task_help"),
            width / 2,
            height - 26,
            0xAAAAAA);
        super.drawScreen(mx, my, partial);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
