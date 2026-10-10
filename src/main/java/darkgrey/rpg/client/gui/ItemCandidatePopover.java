package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.client.TaskPresentationPages;

/** One anchored read-only popover. Hover timing and input ownership are shared by task surfaces. */
public final class ItemCandidatePopover {

    private static NBTTagCompound hovered, owner;
    private static String hoverKey = "", ownerKey = "";
    private static int anchorX, anchorY, panelX, panelY, panelW, panelH, cursor, visible, columns;
    private static long hoverSince, leaveSince;
    private static boolean paged;
    private static NBTTagCompound page;
    private static ItemSlotStrip grid;
    private static String gridKey = "";
    private static final SmoothScroll scroll = new SmoothScroll();
    private static final java.util.List<Integer> cursors = new java.util.ArrayList<Integer>();

    private ItemCandidatePopover() {}

    public static void begin() {
        hovered = null;
    }

    public static boolean offer(NBTTagCompound source, int x, int y, int mouseX, int mouseY) {
        if (!source.getBoolean("group")) return false;
        String key = TaskPresentationPages.identity(source);
        boolean hover = mouseX >= x && mouseX < x + 20 && mouseY >= y && mouseY < y + 20;
        if (hover) {
            hovered = source;
            anchorX = x;
            anchorY = y;
        }
        return hover || owner != null && ownerKey.equals(key);
    }

    public static List<String> draw(int width, int height, int mouseX, int mouseY) {
        long now = System.nanoTime();
        String key = hovered == null ? "" : TaskPresentationPages.identity(hovered);
        if (!key.equals(hoverKey)) {
            hoverKey = key;
            hoverSince = now;
        }
        if (hovered != null && !key.equals(ownerKey)) {
            owner = (NBTTagCompound) hovered.copy();
            ownerKey = key;
            cursor = 0;
            scroll.jump(0);
            cursors.clear();
            grid = null;
        }
        if (owner == null) return null;
        owner.setInteger("operation", owner.getInteger("operation") == 4 ? 4 : 1);
        page = TaskPresentationPages.page(owner, cursor);
        int count = page == null ? Math.min(20, Math.max(1, owner.getInteger("total")))
            : Math.max(
                1,
                page.getTagList("items", 10)
                    .tagCount());
        columns = Math.max(1, Math.min(Math.min(5, count), (width - 24) / 24));
        int rows = Math.max(1, Math.min(Math.min(4, (count + columns - 1) / columns), (height - 58) / 24));
        visible = columns * rows;
        scroll.bounds(Math.max(0, (count + columns - 1) / columns * 24 - rows * 24));
        scroll.tick();
        panelW = columns * 24 + 16;
        panelH = rows * 24 + 48;
        int fullColumns = Math
            .max(1, Math.min(Math.min(5, Math.max(1, Math.min(20, owner.getInteger("total")))), (width - 24) / 24));
        int fullRows = Math.max(
            1,
            Math.min(
                Math.min(4, (Math.max(1, Math.min(20, owner.getInteger("total"))) + fullColumns - 1) / fullColumns),
                (height - 58) / 24));
        paged = owner.getInteger("total") > fullColumns * fullRows;
        int fullW = fullColumns * 24 + 16, fullH = fullRows * 24 + 16 + (paged ? 24 : 0);
        // Keep the hover region stable even when the final page contains fewer icons.
        panelW = fullW;
        panelH = fullH;
        panelX = Math.max(2, Math.min(width - panelW - 2, anchorX));
        panelY = anchorY + 24 + panelH <= height - 2 ? anchorY + 24 : Math.max(2, anchorY - panelH - 4);
        boolean inside = contains(mouseX, mouseY) || hovered != null && key.equals(ownerKey);
        if (inside) leaveSince = now;
        else if (now - leaveSince >= 200000000L) {
            close();
            return null;
        }
        Gui.drawRect(panelX, panelY, panelX + panelW, panelY + panelH, DgrUiPalette.BORDER);
        Gui.drawRect(panelX + 1, panelY + 1, panelX + panelW - 1, panelY + panelH - 1, DgrUiPalette.WINDOW_PANEL);
        Minecraft mc = Minecraft.getMinecraft();

        if (page == null) DgrUiText.left(mc.fontRenderer, "加载中…", panelX + 8, panelY + 8, DgrUiPalette.SECONDARY);
        else {
            NBTTagList values = page.getTagList("items", 10);
            NBTTagList subset = new NBTTagList();
            for (int i = 0; i < values.tagCount(); i++) subset.appendTag(
                values.getCompoundTagAt(i)
                    .copy());
            String currentKey = ownerKey + ":" + cursor + ":" + columns;
            if (grid == null || !gridKey.equals(currentKey)) {
                NBTTagCompound data = new NBTTagCompound();
                data.setTag("items", subset);
                grid = new ItemSlotStrip(data, false);
                gridKey = currentKey;
            }
            grid.draw(
                panelX + 8,
                panelY + 8 - scroll.pixelOffset(),
                columns * 24 - 4,
                panelY + 8,
                panelY + 8 + rows * 24,
                mouseX,
                mouseY);
        }
        if (paged) DgrUiText.left(mc.fontRenderer, "‹", panelX + 10, panelY + panelH - 17, DgrUiPalette.TEXT);
        if (paged) DgrUiText.left(mc.fontRenderer, "›", panelX + panelW - 16, panelY + panelH - 17, DgrUiPalette.TEXT);
        return grid == null ? null : grid.tooltip;
    }

    public static boolean contains(int x, int y) {
        return owner != null && x >= panelX && x < panelX + panelW && y >= panelY && y < panelY + panelH;
    }

    public static boolean click(int x, int y, int button) {
        if (!contains(x, y)) return false;
        if (paged && button == 0 && y >= panelY + panelH - 24) turn(x < panelX + panelW / 2 ? -1 : 1);
        return true;
    }

    public static boolean wheel(int x, int y, int wheel) {
        // A button event still has to reach GuiScreen.mouseClicked.
        if (wheel == 0 || !contains(x, y)) return false;
        int maximum = page == null ? 0
            : Math.max(
                0,
                (page.getTagList("items", 10)
                    .tagCount() + columns
                    - 1) / columns * 24 - visible / columns * 24);
        if (page != null && paged
            && ((wheel < 0 && scroll.pixelOffset() >= maximum) || (wheel > 0 && scroll.pixelOffset() <= 0)))
            turn(wheel < 0 ? 1 : -1);
        else scroll.wheel(wheel, 24);
        return true;
    }

    private static void turn(int direction) {
        if (page == null) return;
        int count = page.getTagList("items", 10)
            .tagCount();
        int maximum = Math.max(0, (count + columns - 1) / columns * 24 - visible / columns * 24);
        if (direction > 0) {
            if (scroll.pixelOffset() < maximum) {
                scroll.jump(Math.min(maximum, scroll.pixelOffset() + visible / columns * 24));
                return;
            } else if (page.getInteger("next") < page.getInteger("total")) {
                cursors.add(cursor);
                cursor = page.getInteger("next");
            } else return;
        } else if (scroll.pixelOffset() > 0) {
            scroll.jump(Math.max(0, scroll.pixelOffset() - visible / columns * 24));
            return;
        } else {
            if (cursors.isEmpty()) return;
            cursor = cursors.remove(cursors.size() - 1);
        }
        grid = null;
        page = null;
        scroll.jump(0);
    }

    public static boolean escape() {
        if (owner == null) return false;
        close();
        return true;
    }

    public static void close() {
        owner = hovered = null;
        ownerKey = hoverKey = "";
        grid = null;
        page = null;
        cursors.clear();
        scroll.jump(0);
    }
}
