package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.input.Mouse;
import org.lwjgl.opengl.GL11;

import darkgrey.rpg.client.ClientPackageManager;
import darkgrey.rpg.client.ClientQuestKeyHandler;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.project.packages.StoryPackageManagerPacket;
import darkgrey.rpg.project.packages.StoryPackageManagerService;

/** Scrollable container directory. Network pages are transport chunks, never UI pages. */
public final class GuiStoryPackageManager extends GuiScreen {

    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(300, 180, 420, 260);
    private final RuntimeDirectoryTree tree = new RuntimeDirectoryTree();
    private final Object connection;
    private NBTTagCompound listing, detail = new NBTTagCompound();
    private final Map<String, NBTTagCompound> containers = new LinkedHashMap<String, NBTTagCompound>();
    private final Map<String, NBTTagCompound> details = new HashMap<String, NBTTagCompound>();
    private final Map<String, NBTTagCompound> graphNodes = new LinkedHashMap<String, NBTTagCompound>();
    private final Map<String, Integer> graphIndexes = new HashMap<String, Integer>();
    private final List<NBTTagCompound> graphEdges = new ArrayList<NBTTagCompound>();
    private final Map<String, double[]> cameras = new HashMap<String, double[]>();
    private final java.util.Set<String> diagnostics = new java.util.HashSet<String>();
    private String session, selected = "", selectedMember = "", message = "", requestedHandle = "";
    private long revision, request, requestedAt, lastSentAt;
    private StoryPackageManagerPacket pendingPacket;
    private int requestedAction, nextListPage = -1;
    private final SmoothScroll listScroll = new SmoothScroll(), detailScroll = new SmoothScroll();
    private boolean initialized, loading, graph, panning, refreshPending, reloadSelection;
    private double zoom = 1, panX = 12, panY = 12;
    private int dragX, dragY;
    private final List<Integer> diagnosticLines = new ArrayList<Integer>();

    public GuiStoryPackageManager(NBTTagCompound snapshot) {
        listing = (NBTTagCompound) snapshot.copy();
        session = snapshot.getString("session");
        revision = snapshot.getLong("revision");
        connection = net.minecraft.client.Minecraft.getMinecraft()
            .getNetHandler();
        appendListing(snapshot);
    }

    @Override
    public void initGui() {
        if (!initialized && UtilityWindowChrome.settings()
            .preference("window.story-packages.width")
            .isEmpty()) {
            geometry.restore(
                width,
                height,
                new UtilityWindowGeometry.Snapshot(
                    .5,
                    .5,
                    Math.min(420, Math.max(300, (int) (width * .8))),
                    Math.min(260, Math.max(180, (int) (height * .8)))));
        } else UtilityWindowChrome.open("story-packages", geometry, width, height, initialized);
        initialized = true;
        buttons();
    }

    private int nav() {
        return Math.min(185, Math.max(110, geometry.width / 3));
    }

    private int top() {
        return geometry.y + 53;
    }

    private int bottom() {
        return geometry.y + geometry.height - 42;
    }

    private int visibleRows() {
        return Math.max(1, (bottom() - top()) / RuntimeDirectoryVisuals.ROW_HEIGHT);
    }

    private NBTTagCompound selectedRow() {
        NBTTagCompound row = containers.get(selected);
        return row == null ? new NBTTagCompound() : row;
    }

    private List<RuntimeDirectoryTree.Row> treeRows() {
        List<RuntimeDirectoryTree.Entry> entries = new ArrayList<RuntimeDirectoryTree.Entry>();
        for (NBTTagCompound row : containers.values()) {
            String handle = row.getString("handle");
            boolean group = row.getBoolean("group");
            NBTTagCompound cached = details.get(handle);
            NBTTagList members = cached == null ? new NBTTagList() : cached.getTagList("members", 10);
            if (group && members.tagCount() > 0) for (int i = 0; i < members.tagCount(); i++) {
                NBTTagCompound member = members.getCompoundTagAt(i);
                entries.add(
                    new RuntimeDirectoryTree.Entry(
                        handle + "|" + member.getString("uid"),
                        member.getString("name"),
                        handle,
                        row.getString("name"),
                        true));
            }
            else entries.add(
                new RuntimeDirectoryTree.Entry(
                    group ? handle + "|" : handle,
                    row.getString("name"),
                    handle,
                    row.getString("name"),
                    group));
        }
        List<RuntimeDirectoryTree.Row> rows = tree.rows(entries, "");
        rows.removeIf(row -> row.depth > 0 && row.key.endsWith("|"));
        return rows;
    }

    private void buttons() {
        buttonList.clear();
        int x = geometry.x, y = geometry.y, right = x + geometry.width;
        boolean container = !selected.isEmpty(), enabled = selectedRow().getBoolean("enabled");
        addAction(4, right - 145, y + 5, "启用", !loading && container && !enabled, GuiRpgButton.Icon.ENABLE);
        addAction(5, right - 98, y + 5, "禁用", !loading && container && enabled, GuiRpgButton.Icon.DISABLE);
        addAction(1, right - 51, y + 5, "刷新", !loading, GuiRpgButton.Icon.REFRESH);
        if (!graph && diagnostics.contains(diagnosticKey())) add(7, right - 66, bottom() + 21, 56, "复制 UID", true);
        if (graph) add(9, right - 53, top() - 23, 43, "适配", true);
    }

    private String diagnosticKey() {
        return selected + "|" + selectedMember;
    }

    private void add(int id, int x, int y, int w, String text, boolean enabled) {
        GuiButton button = new GuiRpgButton(id, x, y, w, 18, text);
        button.enabled = enabled;
        buttonList.add(button);
    }

    private void addAction(int id, int x, int y, String text, boolean enabled, GuiRpgButton.Icon icon) {
        GuiButton button = new GuiRpgButton(id, x, y, 43, 18, text, icon);
        button.enabled = enabled;
        buttonList.add(button);
    }

    private void send(int action, int page) {
        send(action, page, selected);
    }

    private void send(int action, int page, String handle) {
        send(action, page, handle, false);
    }

    private void send(int action, int page, String handle, boolean enabled) {
        if (loading || mc.getNetHandler() != connection) return;
        NBTTagCompound input = new NBTTagCompound();
        input.setInteger("action", action);
        input.setString("session", session);
        input.setLong("revision", revision);
        input.setInteger("page", page);
        input.setString("handle", handle);
        input.setBoolean("enabled", enabled);
        request = ClientPackageManager.next();
        requestedAction = action;
        requestedHandle = handle;
        loading = true;
        requestedAt = System.currentTimeMillis();
        pendingPacket = new StoryPackageManagerPacket(false, request, input);
        dispatchPending(requestedAt);
        buttons();
    }

    private void dispatchPending(long now) {
        if (pendingPacket == null || now - lastSentAt < 150) return;
        DialogueNetwork.CHANNEL.sendToServer(pendingPacket);
        pendingPacket = null;
        lastSentAt = requestedAt = now;
    }

    private void appendListing(NBTTagCompound data) {
        int page = data.getInteger("page");
        if (page == 0) {
            containers.clear();
            for (NBTTagCompound cached : details.values()) {
                cached.setBoolean("complete", false);
                cached.setInteger("page", -1);
            }
        }
        NBTTagList rows = data.getTagList("rows", 10);
        for (int i = 0; i < rows.tagCount(); i++) {
            NBTTagCompound row = rows.getCompoundTagAt(i);
            containers.put(row.getString("handle"), row);
        }
        listing = (NBTTagCompound) data.copy();
        nextListPage = (page + 1) * StoryPackageManagerService.PAGE_SIZE < data.getInteger("total") ? page + 1 : -1;
        if (nextListPage < 0) {
            details.keySet()
                .retainAll(containers.keySet());
            if (!selected.isEmpty() && !containers.containsKey(selected)) {
                selected = selectedMember = "";
                detail = new NBTTagCompound();
                graph = false;
            }
        }
        listScroll.bounds(treeRows().size() * RuntimeDirectoryVisuals.ROW_HEIGHT - (bottom() - top()));
    }

    private static void appendTags(NBTTagCompound target, NBTTagCompound source, String key, int type) {
        NBTTagList values = target.getTagList(key, type), incoming = source.getTagList(key, type);
        for (int i = 0; i < incoming.tagCount(); i++) values.appendTag(
            type == 10 ? incoming.getCompoundTagAt(i)
                .copy() : new net.minecraft.nbt.NBTTagString(incoming.getStringTagAt(i)));
        target.setTag(key, values);
    }

    public void accept(long sequence, NBTTagCompound data) {
        if (mc.getNetHandler() != connection) return;
        if (data.getBoolean("notification")) {
            if (data.getBoolean("denied")) {
                mc.displayGuiScreen(null);
                return;
            }
            if (session.equals(data.getString("session")) && data.getLong("revision") > revision) {
                revision = data.getLong("revision");
                refreshPending = true;
            }
            return;
        }
        if (!loading || sequence != request) return;
        loading = false;
        if (data.getBoolean("denied") || data.getBoolean("closed")) {
            mc.displayGuiScreen(null);
            return;
        }
        if (data.getLong("revision") < revision || !session.equals(data.getString("session"))) {
            refreshPending = true;
            buttons();
            return;
        }
        revision = data.getLong("revision");
        if (!data.getString("error")
            .isEmpty()) message = data.getString("error");
        else if (!data.getString("message")
            .isEmpty()) message = data.getString("message");
        if (data.getBoolean("refresh")) {
            refreshPending = true;
        }
        if (data.hasKey("rows", 9)) {
            if (data.getInteger("page") == 0 && !selected.isEmpty()) reloadSelection = true;
            appendListing(data);
        }
        if (data.hasKey("detail", 10)) {
            String handle = data.getCompoundTag("detail")
                .getString("handle");
            int page = data.getInteger("page");
            NBTTagCompound cached = details.get(handle);
            if (cached == null || page == 0) {
                cached = (NBTTagCompound) data.copy();
                details.put(handle, cached);
            } else {
                appendTags(cached, data, "members", 10);
                appendTags(cached, data, "edges", 10);
                appendTags(cached, data, "diagnostics", 8);
                cached.setInteger("page", page);
            }
            int pages = Math.max(
                (data.getInteger("member_total") + 31) / 32,
                Math.max((data.getInteger("edge_total") + 31) / 32, (data.getInteger("diagnostic_total") + 11) / 12));
            cached.setBoolean("complete", page + 1 >= pages);
            if (handle.equals(selected)) showCached();
        }
        buttons();
    }

    private void saveCamera() {
        NBTTagCompound cached = details.get(selected);
        if (!selected.isEmpty() && cached != null && cached.getBoolean("complete") && !graphNodes.isEmpty())
            cameras.put(selected, new double[] { zoom, panX, panY });
    }

    private void showCached() {
        NBTTagCompound cached = details.get(selected);
        if (cached == null) return;
        detail = cached;
        graphNodes.clear();
        graphIndexes.clear();
        graphEdges.clear();
        NBTTagList members = detail.getTagList("members", 10), edges = detail.getTagList("edges", 10);
        for (int i = 0; i < members.tagCount(); i++) {
            NBTTagCompound row = members.getCompoundTagAt(i);
            graphNodes.put(row.getString("uid"), row);
        }
        for (int i = 0; i < edges.tagCount(); i++) graphEdges.add(edges.getCompoundTagAt(i));
        if (!selectedRow().getBoolean("group") && selectedMember.isEmpty() && members.tagCount() > 0)
            selectedMember = members.getCompoundTagAt(0)
                .getString("uid");
        if (graph && detail.getBoolean("complete") && !cameras.containsKey(selected)) {
            fit();
            saveCamera();
        }
    }

    @Override
    public void updateScreen() {
        long now = System.currentTimeMillis();
        if (mc.thePlayer == null || mc.getNetHandler() != connection) {
            mc.displayGuiScreen(null);
            return;
        }
        dispatchPending(now);
        if (loading && now - requestedAt > 10000) {
            loading = false;
            pendingPacket = null;
            message = "请求超时，请点击刷新。";
            buttons();
            return;
        }
        if (loading) return;
        if (refreshPending) {
            refreshPending = false;
            saveCamera();
            reloadSelection = !selected.isEmpty();
            send(StoryPackageManagerService.LIST, 0);
            return;
        }
        if (nextListPage >= 0) {
            int next = nextListPage;
            nextListPage = -1;
            send(StoryPackageManagerService.LIST, next);
            return;
        }
        if (reloadSelection && !selected.isEmpty()) {
            reloadSelection = false;
            send(StoryPackageManagerService.GRAPH, 0);
            return;
        }
        for (NBTTagCompound row : containers.values()) {
            String handle = row.getString("handle");
            if (!handle.equals(selected) && !tree.isExpanded(handle)) continue;
            NBTTagCompound cached = details.get(handle);
            if (cached == null || !cached.getBoolean("complete")) {
                send(StoryPackageManagerService.GRAPH, cached == null ? 0 : cached.getInteger("page") + 1, handle);
                return;
            }
        }
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        switch (button.id) {
            case 1:
                send(StoryPackageManagerService.RESCAN, 0);
                break;
            case 4:
                send(StoryPackageManagerService.TOGGLE, 0, selected, true);
                break;
            case 5:
                send(StoryPackageManagerService.TOGGLE, 0, selected, false);
                break;
            case 7:
                setClipboardString(selectedMember);
                break;
            case 9:
                fit();
                saveCamera();
                break;
            default:
                break;
        }
    }

    @Override
    protected void keyTyped(char typed, int key) {
        if (key != 0 && (key == 1 || key == ClientQuestKeyHandler.packageKeyCode()
            || key == mc.gameSettings.keyBindInventory.getKeyCode())) close(key);
        else super.keyTyped(typed, key);
    }

    private void close(int key) {
        net.minecraft.client.settings.KeyBinding.setKeyBindState(key, false);
        ClientQuestKeyHandler.clearPackageKey();
        mc.displayGuiScreen(null);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    @Override
    public void onGuiClosed() {
        geometry.end();
        panning = false;
        saveCamera();
        pendingPacket = null;
        if (mc.getNetHandler() == connection && !session.isEmpty()) {
            NBTTagCompound data = new NBTTagCompound();
            data.setInteger("action", StoryPackageManagerService.CLOSE);
            data.setString("session", session);
            DialogueNetwork.CHANNEL
                .sendToServer(new StoryPackageManagerPacket(false, ClientPackageManager.next(), data));
        }
        UtilityWindowChrome.save("story-packages", geometry, width, height);
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (button >= 0 && (button - 100 == ClientQuestKeyHandler.packageKeyCode()
            || button - 100 == mc.gameSettings.keyBindInventory.getKeyCode())) {
            close(button - 100);
            return;
        }
        if (!UtilityWindowChrome.overButton(buttonList, x, y)
            && (geometry.inTitleBar(x, y) || geometry.inResizeGrip(x, y))) {
            geometry.begin(x, y, button);
            return;
        }
        super.mouseClicked(x, y, button);
        if (graph && graphViewport().contains(x, y) && (button == 0 || button == 2)) {
            if (button == 0 && selectGraphNode(x, y)) return;
            panning = true;
            dragX = x;
            dragY = y;
            return;
        }
        if (button != 0 || y < top() || y >= bottom()) return;
        if (x >= geometry.x + 8 && x < geometry.x + nav()) {
            int index = listScroll.rowAt(y - top(), RuntimeDirectoryVisuals.ROW_HEIGHT);
            List<RuntimeDirectoryTree.Row> rows = treeRows();
            if (index < 0 || index >= rows.size()) return;
            RuntimeDirectoryTree.Row row = rows.get(index);
            String[] identity = row.key.split(java.util.regex.Pattern.quote("|"), -1);
            saveCamera();
            String previous = selected;
            selected = identity[0];
            selectedMember = identity.length > 1 ? identity[1] : "";
            if (row.folder) tree.toggle(selected);
            graph = row.folder;
            detailScroll.jump(0);
            message = "";
            if (!selected.equals(previous)) {
                double[] camera = cameras.get(selected);
                if (camera != null) {
                    zoom = camera[0];
                    panX = camera[1];
                    panY = camera[2];
                }
            }
            detail = details.containsKey(selected) ? details.get(selected) : new NBTTagCompound();
            showCached();
            buttons();
        } else if (x >= geometry.x + nav() && x < geometry.x + geometry.width - 10) {
            if (!graph) {
                lines();
                int line = detailScroll.rowAt(y - top(), 12);
                if (diagnosticLines.contains(line)) {
                    if (!diagnostics.remove(diagnosticKey())) diagnostics.add(diagnosticKey());
                    buttons();
                }
            }
        }
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button >= 0) {
            panning = false;
            saveCamera();
            if (geometry.active()) {
                geometry.end();
                UtilityWindowChrome.save("story-packages", geometry, width, height);
            }
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0) return;
        int x = Mouse.getEventX() * width / mc.displayWidth;
        int y = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (y < top() || y >= bottom() || x < geometry.x || x >= geometry.x + geometry.width) return;
        if (geometry.active()) return;
        if (x < geometry.x + nav()) listScroll.wheel(wheel, 2 * RuntimeDirectoryVisuals.ROW_HEIGHT);
        else if (graph) {
            RuntimeGraphViewport viewport = graphViewport();
            if (!viewport.contains(x, y)) return;
            viewport = viewport.zoomAt(x, y, wheel < 0 ? .9 : 1.1);
            zoom = viewport.zoom;
            panX = viewport.panX;
            panY = viewport.panY;
            saveCamera();
        } else detailScroll.wheel(wheel, 36);
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partial) {
        if (geometry.active()) {
            geometry.move(mouseX, mouseY);
            buttons();
        }
        if (panning) {
            panX += mouseX - dragX;
            panY += mouseY - dragY;
            dragX = mouseX;
            dragY = mouseY;
            saveCamera();
        }
        DgrUiPalette.apply();
        drawDefaultBackground();
        drawRect(
            geometry.x,
            geometry.y,
            geometry.x + geometry.width,
            geometry.y + geometry.height,
            DgrUiPalette.WINDOW_PANEL);
        DgrUiText.left(fontRendererObj, "§l故事包管理", geometry.x + 12, geometry.y + 10, DgrUiPalette.STORY_TEXT);
        int[] counts = listing.getIntArray("counts");
        if (counts.length == 4) DgrUiText.left(
            fontRendererObj,
            "启用 " + counts[0] + "  禁用 " + counts[1] + "  冲突 " + counts[2] + "  错误 " + counts[3],
            geometry.x + 12,
            geometry.y + 33,
            DgrUiPalette.SECONDARY);
        drawRect(geometry.x + nav(), top(), geometry.x + nav() + 1, bottom(), DgrUiPalette.BORDER);
        List<RuntimeDirectoryTree.Row> rows = treeRows();
        List<String> tooltip = null;
        listScroll.bounds(rows.size() * RuntimeDirectoryVisuals.ROW_HEIGHT - (bottom() - top()));
        listScroll.tick();
        detailScroll.tick();
        int first = listScroll.pixelOffset() / RuntimeDirectoryVisuals.ROW_HEIGHT;
        try (GuiScrollClip clip = new GuiScrollClip(geometry.x + 8, top(), geometry.x + nav(), bottom())) {
            for (int i = first; i < Math.min(rows.size(), first + visibleRows() + 2); i++) {
                RuntimeDirectoryTree.Row row = rows.get(i);
                int y = top() + i * RuntimeDirectoryVisuals.ROW_HEIGHT - listScroll.pixelOffset();
                String[] identity = row.key.split(java.util.regex.Pattern.quote("|"), -1);
                NBTTagCompound source = containers.get(identity[0]);
                boolean selectedRow = selected.equals(identity[0]) && (row.folder ? selectedMember.isEmpty()
                    : row.depth == 0 || identity.length > 1 && selectedMember.equals(identity[1]));
                boolean hovered = mouseX >= geometry.x + 8 && mouseX < geometry.x + nav()
                    && mouseY >= y
                    && mouseY < y + RuntimeDirectoryVisuals.ROW_HEIGHT;
                boolean statusText = nav() >= 150;
                String status = source == null ? "" : stateName(source.getString("state"));
                int reserved = statusText ? fontRendererObj.getStringWidth(status) + 7 : 11;
                RuntimeDirectoryVisuals.row(
                    fontRendererObj,
                    row.name,
                    row.folder,
                    row.open,
                    row.depth,
                    geometry.x + 8,
                    y,
                    nav() - 12,
                    selectedRow,
                    hovered,
                    reserved);
                if (source != null) {
                    int right = geometry.x + nav() - 8, badge = right - reserved;
                    drawRect(badge, y + 9, badge + 4, y + 13, color(source.getString("state")));
                    if (statusText) RuntimeDirectoryVisuals.text(
                        fontRendererObj,
                        status,
                        badge + 7,
                        RuntimeDirectoryVisuals.textY(fontRendererObj, y),
                        reserved - 7,
                        DgrUiPalette.SECONDARY);
                    if (hovered) {
                        tooltip = new ArrayList<String>();
                        tooltip.add(row.name);
                        tooltip.add(stateName(source.getString("state")));
                        if (row.depth > 0) tooltip.add("继承故事组状态");
                        if (row.folder) tooltip.add(source.getInteger("members") + " 个故事");
                    }
                }
            }
        }
        if (graph) drawGraph();
        else drawLines(lines());
        DgrUiText.left(
            fontRendererObj,
            fontRendererObj.trimStringToWidth(message.isEmpty() ? "成员继承故事组状态" : message, geometry.width - 24),
            geometry.x + 12,
            bottom() + 6,
            DgrUiPalette.SECONDARY);
        super.drawScreen(mouseX, mouseY, partial);
        UtilityWindowChrome.drawGrip(geometry);
        if (tooltip != null) RuntimeDirectoryVisuals.tooltip(fontRendererObj, tooltip, mouseX, mouseY, width, height);
    }

    private void drawLines(List<String> lines) {
        int visible = Math.max(1, (bottom() - top()) / 12);
        detailScroll.bounds(lines.size() * 12 - (bottom() - top()));
        int first = detailScroll.pixelOffset() / 12;
        try (GuiScrollClip clip = new GuiScrollClip(
            geometry.x + nav() + 1,
            top(),
            geometry.x + geometry.width - 8,
            bottom())) {
            for (int i = first; i < Math.min(lines.size(), first + visible + 2); i++) {
                String line = lines.get(i);
                boolean label = line.startsWith("\u0001");
                DgrUiText.left(
                    fontRendererObj,
                    label ? line.substring(1) : line,
                    geometry.x + nav() + 9,
                    top() + i * 12 - detailScroll.pixelOffset(),
                    label || line.startsWith("§l") ? DgrUiPalette.STORY_TEXT : DgrUiPalette.TEXT);
            }
        }
    }

    private List<String> wrapped(String text) {
        return fontRendererObj.listFormattedStringToWidth(text, Math.max(50, geometry.width - nav() - 22));
    }

    private void section(List<String> lines, String title) {
        if (!lines.isEmpty()) lines.add("");
        lines.add("§l" + title);
    }

    private void field(List<String> lines, String label, String text) {
        lines.add("\u0001" + label);
        lines.addAll(wrapped("  " + text));
    }

    private List<String> lines() {
        diagnosticLines.clear();
        List<String> lines = new ArrayList<String>();
        NBTTagCompound row = detail.getCompoundTag("detail");
        if (!row.hasKey("name")) {
            lines.addAll(wrapped(selected.isEmpty() ? "选择故事或故事组查看详情。" : "正在加载故事详情…"));
            return lines;
        }
        NBTTagCompound member = new NBTTagCompound();
        NBTTagList members = detail.getTagList("members", 10);
        for (int i = 0; i < members.tagCount(); i++) if (selectedMember.equals(
            members.getCompoundTagAt(i)
                .getString("uid")))
            member = members.getCompoundTagAt(i);
        section(lines, member.hasKey("name") ? member.getString("name") : row.getString("name"));
        field(lines, "状态", stateName(row.getString("state")));
        if (row.getBoolean("group")) field(lines, "所属故事组", row.getString("name") + "（成员继承状态，启停作用于整组）");
        section(lines, "来源文件");
        lines.addAll(wrapped(row.getString("file")));
        section(lines, "运行规则");
        field(lines, "活动故事实例", String.valueOf(detail.getInteger("active")));
        if (member.hasKey("repeat")) {
            String policy = member.getString("repeat");
            field(lines, "重复规则", "REPEATABLE".equals(policy) ? "无条件重复" : "ONCE".equals(policy) ? "不可重复" : "满足条件后重复");
            field(lines, "启动条件", member.getString("start_rule"));
            section(lines, "资源统计");
            field(lines, "自有资源", String.valueOf(member.getInteger("owned")));
            field(lines, "引用资源", String.valueOf(member.getInteger("references")));
        }
        section(lines, (diagnostics.contains(diagnosticKey()) ? "- " : "+ ") + "诊断详情");
        diagnosticLines.add(lines.size() - 1);
        if (diagnostics.contains(diagnosticKey())) {
            if (member.hasKey("uid")) {
                field(lines, "故事 UID", member.getString("uid"));
                field(lines, "制作版本", member.getString("version"));
                field(lines, "内容指纹", member.getString("fingerprint"));
            }
            NBTTagList issues = detail.getTagList("diagnostics", 8);
            for (int i = 0; i < issues.tagCount(); i++) lines.addAll(wrapped(issues.getStringTagAt(i)));
        }
        return lines;
    }

    private static int color(String state) {
        return "ENABLED".equals(state) ? 0xff49a85d
            : "CONFLICT".equals(state) ? 0xffe84b45 : "ERROR".equals(state) ? 0xffdb8226 : DgrUiPalette.SECONDARY;
    }

    private static String stateName(String state) {
        return "ENABLED".equals(state) ? "已启用"
            : "DISABLED".equals(state) ? "已禁用" : "CONFLICT".equals(state) ? "身份冲突" : "加载错误";
    }

    private int columns() {
        return Math.max(1, (int) Math.ceil(Math.sqrt(graphNodes.size())));
    }

    private double[] point(String uid) {
        if (graphIndexes.size() != graphNodes.size()) {
            graphIndexes.clear();
            int next = 0;
            for (String key : graphNodes.keySet()) graphIndexes.put(key, next++);
        }
        int index = graphIndexes.getOrDefault(uid, -1);
        return index < 0 ? null : new double[] { index % columns() * 150, index / columns() * 75 };
    }

    private void fit() {
        int cols = columns(), rows = (graphNodes.size() + cols - 1) / cols;
        zoom = Math.max(
            0.05,
            Math.min(
                1,
                Math.min(
                    (geometry.width - nav() - 35.0) / (cols * 150),
                    (bottom() - top() - 54.0) / (Math.max(1, rows) * 75))));
        panX = panY = 4;
    }

    private RuntimeGraphViewport graphViewport() {
        int left = geometry.x + nav() + 10, graphTop = top() + 40;
        return new RuntimeGraphViewport(
            left,
            graphTop,
            geometry.x + geometry.width - 10 - left,
            bottom() - graphTop,
            zoom,
            panX,
            panY);
    }

    private boolean selectGraphNode(int x, int y) {
        RuntimeGraphViewport viewport = graphViewport();
        if (!viewport.contains(x, y)) return false;
        double mx = viewport.modelX(x), my = viewport.modelY(y);
        for (String uid : graphNodes.keySet()) {
            double[] p = point(uid);
            if (mx >= p[0] && mx < p[0] + 125 && my >= p[1] && my < p[1] + 40) {
                selectedMember = uid;
                graph = false;
                panning = false;
                detailScroll.jump(0);
                buttons();
                return true;
            }
        }
        return false;
    }

    private void drawGraph() {
        DgrUiText.left(
            fontRendererObj,
            "§l" + fontRendererObj.trimStringToWidth(selectedRow().getString("name"), geometry.width - nav() - 22),
            geometry.x + nav() + 10,
            top(),
            DgrUiPalette.STORY_TEXT);
        DgrUiText.left(
            fontRendererObj,
            stateName(selectedRow().getString("state")) + " · " + graphNodes.size() + " 个故事",
            geometry.x + nav() + 10,
            top() + 13,
            DgrUiPalette.SECONDARY);
        DgrUiText.left(
            fontRendererObj,
            fontRendererObj.trimStringToWidth("Flow 实线 / Logic 虚线 · 拖动平移 / 滚轮缩放", geometry.width - nav() - 22),
            geometry.x + nav() + 10,
            top() + 26,
            DgrUiPalette.SECONDARY);
        RuntimeGraphViewport viewport = graphViewport();
        if (viewport.width == 0 || viewport.height == 0) return;
        int scale = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight).getScaleFactor();
        GL11.glPushAttrib(GL11.GL_SCISSOR_BIT);
        try {
            GL11.glEnable(GL11.GL_SCISSOR_TEST);
            GL11.glScissor(
                viewport.left * scale,
                mc.displayHeight - (viewport.top + viewport.height) * scale,
                viewport.width * scale,
                viewport.height * scale);
            GL11.glPushMatrix();
            try {
                GL11.glTranslated(viewport.left + panX, viewport.top + panY, 0);
                GL11.glScaled(zoom, zoom, 1);
                for (NBTTagCompound edge : graphEdges) {
                    double[] a = point(edge.getString("from")), b = point(edge.getString("to"));
                    if (a == null || b == null) continue;
                    double ax = a[0] + 125, ay = a[1] + 20, bx = b[0], by = b[1] + 20;
                    line(ax, ay, bx, by, "LOGIC".equals(edge.getString("kind")), DgrUiPalette.SECONDARY);
                    double angle = Math.atan2(by - ay, bx - ax);
                    line(
                        bx,
                        by,
                        bx - 7 * Math.cos(angle - .5),
                        by - 7 * Math.sin(angle - .5),
                        false,
                        DgrUiPalette.TEXT);
                    line(
                        bx,
                        by,
                        bx - 7 * Math.cos(angle + .5),
                        by - 7 * Math.sin(angle + .5),
                        false,
                        DgrUiPalette.TEXT);
                }
                for (Map.Entry<String, NBTTagCompound> entry : graphNodes.entrySet()) {
                    double[] p = point(entry.getKey());
                    if (!viewport.intersects(p[0], p[1], 125, 40)) continue;
                    int x = (int) p[0], y = (int) p[1];
                    drawRect(
                        x,
                        y,
                        x + 125,
                        y + 40,
                        entry.getKey()
                            .equals(selectedMember) ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.BORDER);
                    drawRect(x + 1, y + 1, x + 124, y + 39, DgrUiPalette.SUB_PANEL);
                }
            } finally {
                GL11.glPopMatrix();
            }
            // Geometry zoom remains continuous; glyphs are drawn in screen space on the font pixel grid.
            double textScale = darkgrey.rpg.client.session.DialogueFontScale.effective(zoom, scale);
            for (Map.Entry<String, NBTTagCompound> entry : graphNodes.entrySet()) {
                double[] p = point(entry.getKey());
                if (!viewport.intersects(p[0], p[1], 125, 40)
                    || 40 * zoom < fontRendererObj.FONT_HEIGHT * textScale + 2) continue;
                String label = DgrUiText.label(
                    entry.getValue()
                        .getString("name"));
                label = fontRendererObj.trimStringToWidth(label, Math.max(1, (int) (117 * zoom / textScale)));
                DialogueFontDrawing.draw(
                    fontRendererObj,
                    label,
                    viewport.left + panX + (p[0] + 4) * zoom,
                    viewport.top + panY + (p[1] + 4) * zoom,
                    textScale,
                    DgrUiPalette.TEXT);
            }
        } finally {
            GL11.glPopAttrib();
        }
    }

    private void line(double ax, double ay, double bx, double by, boolean dashed, int color) {
        int count = Math.max(1, Math.min(2048, (int) Math.ceil(Math.max(Math.abs(bx - ax), Math.abs(by - ay)))));
        for (int i = 0; i <= count; i++) {
            if (dashed && i % 10 >= 5) continue;
            int x = (int) (ax + (bx - ax) * i / count), y = (int) (ay + (by - ay) * i / count);
            drawRect(x, y, x + 1, y + 1, color);
        }
    }
}
