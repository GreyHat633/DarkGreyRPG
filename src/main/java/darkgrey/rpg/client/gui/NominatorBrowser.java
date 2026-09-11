package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.GuiTextField;
import net.minecraft.client.gui.ScaledResolution;

import org.lwjgl.opengl.GL11;

import darkgrey.rpg.client.NominatorGlobalSearch;
import darkgrey.rpg.nominator.NominatorCatalog;

/** Two clipped lists with a single rendered offset for drawing and hit testing. */
public final class NominatorBrowser extends Gui {

    private static final int ROW_HEIGHT = 20;

    private final FontRenderer font;
    private final NominatorCatalog catalog;
    private final boolean items;
    private int x, y, width, height, split;
    public final GuiTextField search;
    private String query = "", selectedPackage;
    private final SmoothScroll packageMotion = new SmoothScroll();
    private final SmoothScroll resourceMotion = new SmoothScroll();
    private long lastFrame = System.nanoTime();
    private NominatorGlobalSearch.Row selected;
    private List<NominatorGlobalSearch.Row> rows;

    public NominatorBrowser(FontRenderer font, NominatorCatalog catalog, boolean items, int x, int y, int width,
        int height) {
        this.font = font;
        this.catalog = catalog;
        this.items = items;
        search = new GuiTextField(font, x, y, width, 18);
        search.setMaxStringLength(128);
        selectedPackage = catalog.getPackageChoices()
            .isEmpty() ? ""
                : catalog.getPackageChoices()
                    .get(0)
                    .getPackageId();
        refresh();
        layout(x, y, width, height);
    }

    /** Presentation-only: retains search, selection and scroll on drag/resize. */
    public void layout(int x, int y, int width, int height) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        split = width / 3;
        search.xPosition = x;
        search.yPosition = y;
        search.width = width;
        clampScroll();
    }

    private int viewportHeight() {
        return Math.max(0, height - 40);
    }

    private void clampScroll() {
        packageMotion.bounds(
            Math.max(
                0,
                catalog.getPackageChoices()
                    .size() * 20 - viewportHeight()));
        resourceMotion.bounds(Math.max(0, rows.size() * 20 - viewportHeight()));
    }

    private void refresh() {
        rows = NominatorGlobalSearch.search(catalog, selectedPackage, query, items);
        selected = null;
        resourceMotion.jump(0);
        clampScroll();
    }

    public NominatorGlobalSearch.Row selected() {
        return selected;
    }

    public void restore(NominatorBrowser old) {
        if (old == null) return;
        query = old.search.getText();
        search.setText(query);
        search.setFocused(old.search.isFocused());
        if (catalog.getPackageChoice(old.selectedPackage) != null) selectedPackage = old.selectedPackage;
        refresh();
        packageMotion.restore(old.packageMotion);
        resourceMotion.restore(old.resourceMotion);
        if (old.selected != null) for (NominatorGlobalSearch.Row row : rows)
            if (row.id.equals(old.selected.id) && row.type.equals(old.selected.type)
                && row.source.getPackageId()
                    .equals(old.selected.source.getPackageId()))
                selected = row;
    }

    public static String resourceLabel(NominatorGlobalSearch.Row row, boolean global) {
        String type = "NPC".equals(row.type) ? "NPCID" : "Item".equals(row.type) ? "ItemID" : "GroupID";
        return (global ? "[" + row.source.getDisplayName() + "] " : "") + row.name
            + "  "
            + net.minecraft.util.EnumChatFormatting.GRAY
            + "["
            + type
            + "] "
            + row.id;
    }

    public void draw(int mx, int my) {
        if (!query.equals(search.getText())) {
            query = search.getText();
            refresh();
        }
        long now = System.nanoTime();
        double seconds = (now - lastFrame) / 1000000000.0;
        lastFrame = now;
        packageMotion.advance(seconds);
        resourceMotion.advance(seconds);
        search.drawTextBox();
        if (query.isEmpty() && !search.isFocused()) font.drawString(
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.search_hint"),
            x + 4,
            y + 5,
            0x888888);
        drawRect(x, y + 24, x + width, y + height, DgrUiPalette.SUB_PANEL);
        drawRect(x + split, y + 24, x + split + 1, y + height, DgrUiPalette.BORDER);
        font.drawString("故事包", x + 4, y + 27, DgrUiPalette.SECONDARY);
        font.drawString(
            query.trim()
                .isEmpty() ? "资源" : "全局搜索",
            x + split + 5,
            y + 27,
            DgrUiPalette.SECONDARY);
        if (viewportHeight() == 0) return;
        Minecraft mc = Minecraft.getMinecraft();
        int scale = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight).getScaleFactor();
        GL11.glPushAttrib(GL11.GL_SCISSOR_BIT);
        GL11.glEnable(GL11.GL_SCISSOR_TEST);
        GL11.glScissor(
            x * scale,
            mc.displayHeight - (y + height) * scale,
            Math.max(0, width * scale),
            viewportHeight() * scale);
        try {
            List<NominatorCatalog.PackageChoice> packages = catalog.getPackageChoices();
            int first = packageMotion.pixelOffset() / 20;
            for (int i = first; i < packages.size(); i++) {
                int top = y + 40 + i * 20 - packageMotion.pixelOffset();
                if (top >= y + height) break;
                NominatorCatalog.PackageChoice p = packages.get(i);
                if (p.getPackageId()
                    .equals(selectedPackage) || hovered(mx, my, x, x + split, top))
                    drawRect(x, top, x + split, top + 18, DgrUiPalette.HOVER);
                font.drawString(
                    font.trimStringToWidth(p.getDisplayName(), split - 8),
                    x + 4,
                    top + 5,
                    DgrUiPalette.TEXT);
            }
            first = resourceMotion.pixelOffset() / 20;
            for (int i = first; i < rows.size(); i++) {
                int top = y + 40 + i * 20 - resourceMotion.pixelOffset();
                if (top >= y + height) break;
                NominatorGlobalSearch.Row r = rows.get(i);
                if (r == selected || hovered(mx, my, x + split + 1, x + width, top))
                    drawRect(x + split + 1, top, x + width, top + 18, DgrUiPalette.HOVER);
                font.drawString(
                    font.trimStringToWidth(
                        resourceLabel(
                            r,
                            !query.trim()
                                .isEmpty()),
                        width - split - 10),
                    x + split + 5,
                    top + 5,
                    DgrUiPalette.TEXT);
            }
            if (rows.isEmpty()) font.drawString("无匹配资源", x + split + 5, y + 40, DgrUiPalette.SECONDARY);
        } finally {
            GL11.glPopAttrib();
        }
    }

    private boolean hovered(int mx, int my, int left, int right, int top) {
        return mx >= left && mx < right && my >= Math.max(y + 40, top) && my < Math.min(y + height, top + ROW_HEIGHT);
    }

    public void click(int mx, int my, int button) {
        search.mouseClicked(mx, my, button);
        if (button != 0 || mx < x || mx >= x + width || my < y + 40 || my >= y + height) return;
        if (mx < x + split) {
            int i = packageMotion.rowAt(my - y - 40);
            if (i < catalog.getPackageChoices()
                .size()) {
                selectedPackage = catalog.getPackageChoices()
                    .get(i)
                    .getPackageId();
                search.setText("");
                query = "";
                refresh();
            }
        } else {
            int i = resourceMotion.rowAt(my - y - 40);
            if (i >= rows.size()) return;
            selected = rows.get(i);
            selectedPackage = selected.source.getPackageId();
            int index = catalog.getPackageChoices()
                .indexOf(selected.source);
            if (index >= 0 && (index * 20 < packageMotion.position()
                || (index + 1) * 20 > packageMotion.position() + viewportHeight())) packageMotion.jump(index * 20);
        }
    }

    public void scroll(int mx, int my, int delta) {
        if (mx < x || mx >= x + width || my < y + 40 || my >= y + height || delta == 0) return;
        if (mx < x + split) packageMotion.wheel(delta);
        else resourceMotion.wheel(delta);
    }
}
