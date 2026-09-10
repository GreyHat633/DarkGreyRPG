package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.GuiTextField;

import darkgrey.rpg.client.NominatorGlobalSearch;
import darkgrey.rpg.nominator.NominatorCatalog;

/** Small two-list/search helper. No binding or inventory mechanics live here. */
public final class NominatorBrowser extends Gui {

    private final FontRenderer font;
    private final NominatorCatalog catalog;
    private final boolean items;
    private final int x, y, width, height, split;
    public final GuiTextField search;
    private String query = "", selectedPackage;
    private int packageScroll, resourceScroll;
    private NominatorGlobalSearch.Row selected;
    private List<NominatorGlobalSearch.Row> rows;

    public NominatorBrowser(FontRenderer font, NominatorCatalog catalog, boolean items, int x, int y, int width,
        int height) {
        this.font = font;
        this.catalog = catalog;
        this.items = items;
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        split = width / 3;
        search = new GuiTextField(font, x, y, width, 18);
        search.setMaxStringLength(128);
        selectedPackage = catalog.getPackageChoices()
            .isEmpty() ? ""
                : catalog.getPackageChoices()
                    .get(0)
                    .getPackageId();
        refresh();
    }

    private void refresh() {
        rows = NominatorGlobalSearch.search(catalog, selectedPackage, query, items);
        selected = null;
        resourceScroll = 0;
    }

    public NominatorGlobalSearch.Row selected() {
        return selected;
    }

    public void restore(NominatorBrowser old) {
        if (old == null) return;
        query = old.search.getText();
        search.setText(query);
        if (catalog.getPackageChoice(old.selectedPackage) != null) selectedPackage = old.selectedPackage;
        refresh();
        packageScroll = Math.max(
            0,
            Math.min(
                old.packageScroll,
                Math.max(
                    0,
                    catalog.getPackageChoices()
                        .size() - count())));
        resourceScroll = Math.max(0, Math.min(old.resourceScroll, Math.max(0, rows.size() - count())));
        if (old.selected != null) for (NominatorGlobalSearch.Row row : rows)
            if (row.id.equals(old.selected.id) && row.type.equals(old.selected.type)
                && row.source.getPackageId()
                    .equals(old.selected.source.getPackageId()))
                selected = row;
    }

    public static String resourceLabel(darkgrey.rpg.client.NominatorGlobalSearch.Row row, boolean global) {
        String type = "NPC".equals(row.type) ? "NPCID" : "Item".equals(row.type) ? "ItemID" : "GroupID";
        return (global ? "[" + row.source.getDisplayName() + "] " : "") + row.name
            + "  "
            + net.minecraft.util.EnumChatFormatting.GRAY
            + "["
            + type
            + "] "
            + row.id;
    }

    private int count() {
        return Math.max(1, (height - 34) / 20);
    }

    public void draw(int mx, int my) {
        if (!query.equals(search.getText())) {
            query = search.getText();
            refresh();
        }
        search.drawTextBox();
        if (query.isEmpty() && !search.isFocused()) font.drawString(
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.search_hint"),
            x + 4,
            y + 5,
            0x888888);
        drawRect(x, y + 24, x + width, y + height, 0xFF202020);
        drawRect(x + split, y + 24, x + split + 1, y + height, 0xFF777777);
        font.drawString(
            query.trim()
                .isEmpty() ? net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.resources")
                    : net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.global_search"),
            x + split + 5,
            y + 23,
            0xAAAAAA);
        List<NominatorCatalog.PackageChoice> packages = catalog.getPackageChoices();
        for (int i = 0; i < count(); i++) {
            int top = y + 34 + i * 20;
            if (i + packageScroll < packages.size()) {
                NominatorCatalog.PackageChoice p = packages.get(i + packageScroll);
                if (p.getPackageId()
                    .equals(selectedPackage)) drawRect(x, top, x + split, top + 18, 0xFF505050);
                font.drawString(
                    font.trimStringToWidth(p.getDisplayName(), split - 8),
                    x + 4,
                    top + 5,
                    DgrUiPalette.TEXT);

            }
            if (i + resourceScroll < rows.size()) {
                NominatorGlobalSearch.Row r = rows.get(i + resourceScroll);
                if (r == selected) drawRect(x + split + 1, top, x + width, top + 18, 0xFF505050);
                int rx = x + split + 5, rw = width - split - 10;
                font.drawString(
                    font.trimStringToWidth(
                        resourceLabel(
                            r,
                            !query.trim()
                                .isEmpty()),
                        rw),
                    rx,
                    top + 5,
                    DgrUiPalette.TEXT);
            }
        }
        if (rows.isEmpty()) font.drawString(
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.no_match"),
            x + split + 5,
            y + 40,
            0xAAAAAA);
    }

    public void click(int mx, int my, int button) {
        search.mouseClicked(mx, my, button);
        if (button != 0 || mx < x || mx >= x + width || my < y + 34 || my >= y + 34 + count() * 20) return;
        int row = (my - y - 34) / 20;
        if (mx < x + split) {
            int i = packageScroll + row;
            if (i < catalog.getPackageChoices()
                .size()) {
                selectedPackage = catalog.getPackageChoices()
                    .get(i)
                    .getPackageId();
                search.setText("");
                query = "";
                refresh();
            }
        } else if (resourceScroll + row < rows.size()) {
            selected = rows.get(resourceScroll + row);
            selectedPackage = selected.source.getPackageId();
            int index = catalog.getPackageChoices()
                .indexOf(selected.source);
            if (index < packageScroll || index >= packageScroll + count()) packageScroll = index;
        }
    }

    public void scroll(int mx, int my, int delta) {
        if (mx < x || mx >= x + width || my < y + 24 || my >= y + height || delta == 0) return;
        int step = delta < 0 ? 1 : -1;
        if (mx < x + split) packageScroll = Math.max(
            0,
            Math.min(
                Math.max(
                    0,
                    catalog.getPackageChoices()
                        .size() - count()),
                packageScroll + step));
        else resourceScroll = Math.max(0, Math.min(Math.max(0, rows.size() - count()), resourceScroll + step));
    }
}
