package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.GuiTextField;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

import darkgrey.rpg.diagnostics.PlayerStateInspection;
import darkgrey.rpg.diagnostics.PlayerStatePacket;
import darkgrey.rpg.network.DialogueNetwork;

/** Administrator-only read-only state viewer. No repair, reset, or player simulation controls. */
public final class GuiPlayerStateInspection extends GuiScreen {

    private static long sequence;
    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(300, 220, 650, 360);
    private boolean initialized, loading;
    private boolean technicalExpanded;
    private GuiTextField name;
    private NBTTagCompound snapshot = new NBTTagCompound();
    private long request, requestedAt;
    private int page, selected, detailScroll, listScroll;
    private String message = "输入本服玩家 ID / 玩家名，查询在线或离线记录。";

    @Override
    public void initGui() {
        UtilityWindowChrome.open("player-state-inspection", geometry, width, height, initialized);
        initialized = true;
        layoutControls();
    }

    private void layoutControls() {
        String text = name == null ? "" : name.getText();
        boolean focused = name == null || name.isFocused();
        int cursor = name == null ? 0 : name.getCursorPosition();
        int selection = name == null ? 0 : name.getSelectionEnd();
        name = new GuiTextField(fontRendererObj, geometry.x + 12, geometry.y + 38, geometry.width - 92, 18);
        name.setMaxStringLength(16);
        name.setText(text);
        name.setCursorPosition(cursor);
        name.setSelectionPos(selection);
        name.setFocused(focused);
        layoutButtons();
    }

    private void layoutButtons() {
        buttonList.clear();
        GuiButton query = new GuiRpgButton(
            1,
            geometry.x + geometry.width - 72,
            geometry.y + 36,
            60,
            20,
            loading ? "查询中" : "查询");
        query.enabled = !loading;
        buttonList.add(query);
        buttonList.add(new GuiRpgButton(2, geometry.x + 12, geometry.y + geometry.height - 27, 64, 20, "上一页"));
        buttonList.add(new GuiRpgButton(3, geometry.x + 82, geometry.y + geometry.height - 27, 64, 20, "下一页"));
        ((GuiButton) buttonList.get(1)).enabled = !loading && page > 0;
        ((GuiButton) buttonList.get(2)).enabled = !loading
            && (page + 1) * PlayerStateInspection.PAGE_SIZE < snapshot.getInteger("total");
        buttonList.add(
            new GuiRpgButton(
                4,
                geometry.x + geometry.width - 100,
                geometry.y + geometry.height - 27,
                88,
                20,
                technicalExpanded ? "收起技术详情" : "技术详情"));
        ((GuiButton) buttonList.get(3)).enabled = selected < rows().tagCount() && rows().getCompoundTagAt(selected)
            .hasKey("summary", 9);
    }

    private void query(int targetPage) {
        if (loading) return;
        if (!name.getText()
            .matches("[A-Za-z0-9_]{1,16}")) {
            message = "请输入有效的玩家名。";
            return;
        }
        page = Math.max(0, targetPage);
        request = ++sequence;
        loading = true;
        requestedAt = System.currentTimeMillis();
        NBTTagCompound data = new NBTTagCompound();
        data.setString("name", name.getText());
        data.setInteger("page", page);
        DialogueNetwork.CHANNEL.sendToServer(new PlayerStatePacket(1, request, data));
        message = "正在读取服务器状态……";
        layoutButtons();
    }

    public void accept(long response, NBTTagCompound data) {
        if (!loading || response != request) return;
        snapshot = (NBTTagCompound) data.copy();
        loading = false;
        selected = 0;
        technicalExpanded = false;
        detailScroll = 0;
        listScroll = 0;
        message = data.getString("error");
        if (message.isEmpty()) message = data.getString("message");
        if (message.isEmpty()) message = data.getString("name") + (data.getBoolean("online") ? " · 在线" : " · 离线")
            + " · "
            + data.getInteger("total")
            + " 条记录";
        layoutButtons();
    }

    @Override
    public void updateScreen() {
        name.updateCursorCounter();
        if (loading && System.currentTimeMillis() - requestedAt > 10000) {
            loading = false;
            message = "查询超时或权限不足，可重新查询。";
            layoutButtons();
        }
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 4) {
            technicalExpanded = !technicalExpanded;
            detailScroll = 0;
            layoutButtons();
        }
        if (button.id == 1) query(0);
        if (button.id == 2 && page > 0) query(page - 1);
        if (button.id == 3 && (page + 1) * PlayerStateInspection.PAGE_SIZE < snapshot.getInteger("total"))
            query(page + 1);
    }

    @Override
    protected void keyTyped(char character, int code) {
        if (code == Keyboard.KEY_ESCAPE) {
            mc.displayGuiScreen(null);
            return;
        }
        if (code == Keyboard.KEY_RETURN || code == Keyboard.KEY_NUMPADENTER) {
            query(0);
            return;
        }
        name.textboxKeyTyped(character, code);
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (geometry.inTitleBar(x, y) || geometry.inResizeGrip(x, y)) {
            geometry.begin(x, y, button);
            return;
        }
        name.mouseClicked(x, y, button);
        super.mouseClicked(x, y, button);
        int top = geometry.y + 102, nav = Math.max(100, geometry.width / 3);
        if (button == 0 && x >= geometry.x + 12
            && x < geometry.x + nav
            && y >= top
            && y < geometry.y + geometry.height - 35) {
            int index = listScroll + (y - top) / 24;
            if (index < rows().tagCount()) {
                selected = index;
                detailScroll = 0;
                technicalExpanded = false;
                layoutButtons();
            }
        }
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button >= 0 && geometry.active()) {
            geometry.end();
            UtilityWindowChrome.save("player-state-inspection", geometry, width, height);
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0) return;
        int x = Mouse.getEventX() * width / mc.displayWidth;
        if (x < geometry.x + Math.max(100, geometry.width / 3)) listScroll = Math
            .max(0, Math.min(Math.max(0, rows().tagCount() - visibleRows()), listScroll + (wheel < 0 ? 1 : -1)));
        else detailScroll = Math
            .max(0, Math.min(Math.max(0, detailLines().size() - detailVisible()), detailScroll + (wheel < 0 ? 3 : -3)));
    }

    private NBTTagList rows() {
        return snapshot.getTagList("rows", 10);
    }

    private int visibleRows() {
        return Math.max(1, (geometry.height - 142) / 24);
    }

    private int detailVisible() {
        return Math.max(1, (geometry.height - 148) / 12);
    }

    private List<String> detailLines() {
        List<String> lines = new ArrayList<String>();
        if (selected >= rows().tagCount()) return lines;
        NBTTagCompound row = rows().getCompoundTagAt(selected);
        NBTTagList detail = row.getTagList(row.hasKey("summary", 9) ? "summary" : "details", 8);
        int wrap = geometry.width - Math.max(100, geometry.width / 3) - 34;
        for (int i = 0; i < detail.tagCount(); i++)
            for (Object line : fontRendererObj.listFormattedStringToWidth(detail.getStringTagAt(i), Math.max(80, wrap)))
                lines.add((String) line);
        if (technicalExpanded && row.hasKey("summary", 9)) {
            lines.add("");
            lines.add("技术详情");
            NBTTagList raw = row.getTagList("details", 8);
            for (int i = 0; i < raw.tagCount(); i++) for (Object line : fontRendererObj
                .listFormattedStringToWidth(raw.getStringTagAt(i), Math.max(80, wrap))) lines.add((String) line);
        }
        return lines;
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partial) {
        if (geometry.active()) {
            geometry.move(mouseX, mouseY);
            layoutControls();
        }
        drawRect(geometry.x, geometry.y, geometry.x + geometry.width, geometry.y + geometry.height, DgrUiPalette.PANEL);
        fontRendererObj.drawString("玩家状态检查 · 只读", geometry.x + 10, geometry.y + 6, DgrUiPalette.TEXT);
        fontRendererObj.drawString("玩家 ID / 玩家名", geometry.x + 12, geometry.y + 25, DgrUiPalette.TEXT);
        name.drawTextBox();
        List<?> summary = fontRendererObj.listFormattedStringToWidth(message, geometry.width - 24);
        for (int i = 0; i < Math.min(2, summary.size()); i++) fontRendererObj
            .drawString((String) summary.get(i), geometry.x + 12, geometry.y + 64 + i * 11, DgrUiPalette.SECONDARY);
        int nav = Math.max(100, geometry.width / 3), top = geometry.y + 102;
        fontRendererObj.drawString("该玩家的内容", geometry.x + 12, top - 13, DgrUiPalette.TEXT);
        fontRendererObj.drawString("状态详情", geometry.x + nav + 12, top - 13, DgrUiPalette.TEXT);
        drawRect(
            geometry.x + nav,
            top - 2,
            geometry.x + nav + 1,
            geometry.y + geometry.height - 34,
            DgrUiPalette.SECONDARY);
        for (int i = listScroll; i < Math.min(rows().tagCount(), listScroll + visibleRows()); i++) {
            int y = top + (i - listScroll) * 24;
            drawRect(
                geometry.x + 9,
                y,
                geometry.x + nav - 4,
                y + 21,
                i == selected ? DgrUiPalette.SELECTED_FILL : DgrUiPalette.SUB_PANEL);
            fontRendererObj.drawString(
                fontRendererObj.trimStringToWidth(
                    rows().getCompoundTagAt(i)
                        .getString("title"),
                    nav - 24),
                geometry.x + 13,
                y + 6,
                DgrUiPalette.TEXT);
        }
        List<String> lines = detailLines();
        drawRect(
            geometry.x + nav + 5,
            top - 2,
            geometry.x + geometry.width - 9,
            geometry.y + geometry.height - 34,
            DgrUiPalette.SUB_PANEL);
        for (int i = detailScroll; i < Math.min(lines.size(), detailScroll + detailVisible()); i++) {
            String line = lines.get(i);
            boolean heading = java.util.Arrays.asList("当前情况", "正在等待什么", "再次启动条件", "发现的问题", "技术详情")
                .contains(line);
            fontRendererObj.drawString(
                (heading ? "\u00a7l" : "") + line,
                geometry.x + nav + 12,
                top + (i - detailScroll) * 12,
                DgrUiPalette.TEXT);
        }
        fontRendererObj.drawString(
            "第 " + (page + 1) + " 页",
            geometry.x + 155,
            geometry.y + geometry.height - 21,
            DgrUiPalette.SECONDARY);
        if (snapshot.hasKey("queried", 4) && geometry.width >= 560) {
            String queried = new java.text.SimpleDateFormat("HH:mm:ss")
                .format(new java.util.Date(snapshot.getLong("queried")));
            fontRendererObj.drawString(
                "查询时间 " + queried,
                geometry.x + geometry.width - 230,
                geometry.y + geometry.height - 21,
                DgrUiPalette.SECONDARY);
        }
        super.drawScreen(mouseX, mouseY, partial);
        UtilityWindowChrome.drawGrip(geometry);
    }

    @Override
    public void onGuiClosed() {
        UtilityWindowChrome.save("player-state-inspection", geometry, width, height);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
