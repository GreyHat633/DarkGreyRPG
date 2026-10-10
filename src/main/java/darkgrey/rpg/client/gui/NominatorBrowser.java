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

    private static final int ROW_HEIGHT = RuntimeDirectoryVisuals.ROW_HEIGHT;
    private final RuntimeDirectoryTree tree = new RuntimeDirectoryTree();
    private List<RuntimeDirectoryTree.Row> directory = new java.util.ArrayList<RuntimeDirectoryTree.Row>();

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
    private List<String> tooltip;
    private final RuntimeRowText.Metrics metrics;

    public NominatorBrowser(FontRenderer font, NominatorCatalog catalog, boolean items, int x, int y, int width,
        int height) {
        this.font = font;
        metrics = RuntimeDirectoryVisuals.metrics(font);
        this.catalog = catalog;
        this.items = items;
        search = new GuiDgrTextField(font, x, y, width, 18);
        search.setMaxStringLength(128);
        selectedPackage = catalog.getPackageChoices()
            .isEmpty() ? ""
                : catalog.getPackageChoices()
                    .get(0)
                    .getPackageId();
        NominatorCatalog.PackageChoice initial = catalog.getPackageChoice(selectedPackage);
        if (initial != null && initial.isGroup()) tree.toggle(initial.getContainerId());
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
        return Math.max(0, height - 42);
    }

    private void clampScroll() {
        packageMotion.bounds(Math.max(0, directory.size() * ROW_HEIGHT - viewportHeight()));
        resourceMotion.bounds(Math.max(0, rows.size() * ROW_HEIGHT - viewportHeight()));
    }

    private void refresh() {
        rows = NominatorGlobalSearch.search(catalog, selectedPackage, query, items);
        selected = null;
        resourceMotion.jump(0);
        java.util.List<RuntimeDirectoryTree.Entry> entries = new java.util.ArrayList<RuntimeDirectoryTree.Entry>();
        java.util.Set<String> matchingPackages = new java.util.HashSet<String>();
        for (NominatorGlobalSearch.Row row : rows) matchingPackages.add(row.source.getPackageId());
        for (NominatorCatalog.PackageChoice choice : catalog.getPackageChoices()) {
            if (!query.trim()
                .isEmpty() && !matchingPackages.contains(choice.getPackageId())) continue;
            entries.add(
                new RuntimeDirectoryTree.Entry(
                    choice.getPackageId(),
                    choice.getDisplayName(),
                    choice.getContainerId(),
                    choice.getContainerName(),
                    choice.isGroup()));
        }
        directory = tree.rows(
            entries,
            "",
            !query.trim()
                .isEmpty());
        clampScroll();
    }

    public NominatorGlobalSearch.Row selected() {
        return selected;
    }

    public void restore(NominatorBrowser old) {
        if (old == null) return;
        tree.restore(old.tree);
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
        String type = "NPC".equals(row.type) ? "角色"
            : "Group".equals(row.type) ? "角色组" : "Item".equals(row.type) ? "物品" : "物品组";
        return "[" + type + "] " + row.name + (row.source.isReference(row.id) ? " [引用]" : "");
    }

    public static String sourceLabel(NominatorGlobalSearch.Row row, boolean global, NominatorCatalog catalog) {
        return row.source.isReference(row.id) ? "来源：" + catalog.ownerName(row.id)
            : global ? "所属：" + row.source.getDisplayName() : "";
    }

    public List<String> tooltip() {
        return tooltip;
    }

    public void draw(int mx, int my) {
        tooltip = null;
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
        if (query.isEmpty() && !search.isFocused())
            RuntimeDirectoryVisuals.text(font, "名称 / 标签", x + 4, y + 5, width - 8, 0x888888);
        drawRect(x, y + 24, x + width, y + height, DgrUiPalette.SUB_PANEL);
        drawRect(x + split, y + 24, x + split + 1, y + height, DgrUiPalette.BORDER);
        RuntimeDirectoryVisuals.heading(font, "故事包", x + 4, y + 25, split - 8);
        RuntimeDirectoryVisuals.heading(font, "资源", x + split + 5, y + 25, width - split - 10);
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
            int first = packageMotion.pixelOffset() / ROW_HEIGHT;
            for (int i = first; i < directory.size(); i++) {
                int top = y + 42 + i * ROW_HEIGHT - packageMotion.pixelOffset();
                if (top >= y + height) break;
                RuntimeDirectoryTree.Row row = directory.get(i);
                RuntimeDirectoryVisuals.row(
                    font,
                    row.name,
                    row.folder,
                    row.open,
                    row.depth,
                    x,
                    top,
                    split,
                    row.key.equals(selectedPackage),
                    hovered(mx, my, x, x + split, top, ROW_HEIGHT));
                if (hovered(mx, my, x, x + split, top, ROW_HEIGHT))
                    tooltip = java.util.Collections.singletonList(row.name);
            }
            first = resourceMotion.pixelOffset() / ROW_HEIGHT;
            for (int i = first; i < rows.size(); i++) {
                int top = y + 42 + i * ROW_HEIGHT - resourceMotion.pixelOffset();
                if (top >= y + height) break;
                NominatorGlobalSearch.Row r = rows.get(i);
                boolean hovered = hovered(mx, my, x + split + 1, x + width, top, ROW_HEIGHT);
                if (r == selected || hovered) drawRect(
                    x + split + 1,
                    top,
                    x + width,
                    top + ROW_HEIGHT,
                    r == selected ? DgrUiPalette.SELECTED_FILL : DgrUiPalette.HOVER);
                boolean global = !query.trim()
                    .isEmpty();
                String label = resourceLabel(r, global), source = sourceLabel(r, global, catalog);
                RuntimeRowText.ResourceLayout layout = RuntimeRowText
                    .resource(label, r.source.isReference(r.id), source, width - split - 10, metrics);
                int left = x + split + 5, textY = RuntimeDirectoryVisuals.textY(font, top);
                RuntimeDirectoryVisuals.text(font, layout.name, left, textY, layout.nameWidth, DgrUiPalette.TEXT);
                RuntimeDirectoryVisuals.text(
                    font,
                    layout.source,
                    left + layout.sourceOffset,
                    textY,
                    width - split - 10 - layout.sourceOffset,
                    DgrUiPalette.SECONDARY);
                if (hovered) {
                    tooltip = new java.util.ArrayList<String>();
                    tooltip.add(label);
                    if (!source.isEmpty()) tooltip.add(source);
                }
            }
            if (rows.isEmpty()) RuntimeDirectoryVisuals.text(
                font,
                "无匹配资源",
                x + split + 5,
                RuntimeDirectoryVisuals.textY(font, y + 42),
                width - split - 10,
                DgrUiPalette.SECONDARY);
        } finally {
            GL11.glPopAttrib();
        }
    }

    private boolean hovered(int mx, int my, int left, int right, int top, int rowHeight) {
        return mx >= left && mx < right && my >= Math.max(y + 42, top) && my < Math.min(y + height, top + rowHeight);
    }

    public void click(int mx, int my, int button) {
        search.mouseClicked(mx, my, button);
        if (button != 0 || mx < x || mx >= x + width || my < y + 42 || my >= y + height) return;
        if (mx < x + split) {
            int i = (my - y - 42 + packageMotion.pixelOffset()) / ROW_HEIGHT;
            if (i >= 0 && i < directory.size()) {
                RuntimeDirectoryTree.Row row = directory.get(i);
                if (row.folder) {
                    tree.toggle(row.key);
                    NominatorGlobalSearch.Row keep = selected;
                    SmoothScroll position = new SmoothScroll();
                    position.restore(resourceMotion);
                    refresh();
                    selected = keep;
                    resourceMotion.restore(position);
                    clampScroll();
                    return;
                }
                selectedPackage = row.key;
                search.setText("");
                query = "";
                refresh();
            }
        } else {
            int i = (my - y - 42 + resourceMotion.pixelOffset()) / ROW_HEIGHT;
            if (i >= rows.size()) return;
            selected = rows.get(i);
            selectedPackage = selected.source.getPackageId();

        }
    }

    public void scroll(int mx, int my, int delta) {
        if (mx < x || mx >= x + width || my < y + 40 || my >= y + height || delta == 0) return;
        if (mx < x + split) packageMotion.wheel(delta);
        else resourceMotion.wheel(delta);
    }
}
