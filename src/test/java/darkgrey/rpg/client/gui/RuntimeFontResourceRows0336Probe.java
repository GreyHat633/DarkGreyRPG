package darkgrey.rpg.client.gui;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.client.NominatorGlobalSearch;
import darkgrey.rpg.client.session.DialogueFontScaleProbe;
import darkgrey.rpg.nominator.NominatorCatalog;

/** Native font advances, source allocation, scoped identity and scrolling; no GL or disk mutation. */
public final class RuntimeFontResourceRows0336Probe {

    public static void main(String[] args) throws Exception {
        JsonObject profile;
        try (java.io.Reader reader = Files
            .newBufferedReader(Paths.get("schema/dialogue-capacity-profile.json"), StandardCharsets.UTF_8)) {
            profile = new JsonParser().parse(reader)
                .getAsJsonObject();
        }
        for (String mode : new String[] { "normal", "unicode" }) {
            RuntimeRowText.Metrics metrics = metrics(profile.getAsJsonArray(mode));
            for (int width = 0; width <= 640; width++) {
                for (String source : new String[] { "", "来源：故事A", "来源：这是一个很长的故事名称 ABC 123" }) {
                    for (boolean reference : new boolean[] { false, true }) {
                        String label = "[角色组] 很长的资源名称 English 123 酒馆老板" + (reference ? " [引用]" : "");
                        RuntimeRowText.ResourceLayout row = RuntimeRowText
                            .resource(label, reference, source, width, metrics);
                        check(metrics.width(row.name) <= row.nameWidth, "name remains in its measured bounds");
                        int sourceWidth = metrics.width(row.source);
                        check(sourceWidth <= width * 2 / 5, "source uses at most forty percent");
                        check(row.sourceOffset + sourceWidth == width, "source right aligns exactly");
                        if (sourceWidth > 0) {
                            check(row.nameWidth >= 64, "source never consumes minimum name budget");
                            check(row.sourceOffset - row.nameWidth == 8, "eight pixel gap");
                        }
                        if (reference && row.nameWidth >= metrics.width(" [引用]"))
                            check(row.name.endsWith("[引用]"), "truncation preserves reference marker");
                        if (source.isEmpty()) check(
                            row.nameWidth == width && row.source.isEmpty(),
                            "owned row has no empty source reservation");
                    }
                }
            }
            for (String text : new String[] { "中文 English 123", "§l加粗文件夹 ABC 123", "来源：故事A" }) {
                for (int width = 1; width <= 220; width++) {
                    String clipped = RuntimeRowText.fit(text, width, metrics);
                    check(metrics.width(clipped) <= width, "ellipsis includes its own native advance");
                    if (metrics.width(text) <= width) check(clipped.equals(text), "fitting text is unchanged");
                }
            }
        }
        catalog();
        scrolling();
        DialogueFontScaleProbe.main(new String[0]);
        System.out.println(
            "RUNTIME_FONT_RESOURCE_ROWS_0336_PASS: native advances, narrow/long rows, reference marker,"
                + " right aligned source, four resource types, same-name scoped search, 22px scrolling and pixel grid");
    }

    private static RuntimeRowText.Metrics metrics(final JsonArray advances) {
        return new RuntimeRowText.Metrics() {

            public int width(String text) {
                int width = 0;
                boolean bold = false;
                for (int i = 0; i < text.length(); i++) {
                    char c = text.charAt(i);
                    if (c == '§' && i + 1 < text.length()) {
                        char format = text.charAt(++i);
                        if (format == 'l') bold = true;
                        else if (format == 'r' || "0123456789abcdef".indexOf(format) >= 0) bold = false;
                    } else {
                        int advance = advances.get(c)
                            .getAsInt();
                        width += advance + (bold && advance > 0 ? 1 : 0);
                    }
                }
                return width;
            }

            public String trim(String text, int limit) {
                int end = 0;
                for (int i = 0; i < text.length(); i++) {
                    if (text.charAt(i) == '§' && i + 1 < text.length()) i++;
                    if (width(text.substring(0, i + 1)) > limit) break;
                    end = i + 1;
                }
                return text.substring(0, end);
            }
        };
    }

    private static void catalog() {
        String a = "ST-2345-6789-ABCD-EFGH", b = "ST-3456-789A-BCDE-FGHJ";
        String actor = a + "~actor~a", group = b + "~actor~b", item = a + "~item~c", itemGroup = b + "~item_group~d";
        List<String> empty = Collections.emptyList();
        NominatorCatalog.PackageChoice own = new NominatorCatalog.PackageChoice(
            a,
            a,
            "来源故事A",
            Collections.singletonList(actor),
            Collections.singletonList(item),
            empty);
        NominatorCatalog.PackageChoice borrowed = new NominatorCatalog.PackageChoice(
            b,
            b,
            "所属故事B",
            Arrays.asList(actor, group),
            Collections.singletonList(item),
            Collections.singletonList(itemGroup),
            "组",
            "故事组",
            true,
            Arrays.asList(actor, item));
        NominatorCatalog catalog = new NominatorCatalog(
            Arrays.asList(
                new NominatorCatalog.Story(a, "来源故事A", "", empty),
                new NominatorCatalog.Story(b, "所属故事B", "", empty)),
            Arrays.asList(
                new NominatorCatalog.Actor(actor, "同名", "individual", a, "", empty),
                new NominatorCatalog.Actor(group, "同名", "collective", b, "", empty)),
            Collections.singletonList(new NominatorCatalog.Item(item, "铜币", empty)),
            Collections.singletonList(new NominatorCatalog.Item(itemGroup, "剑", empty)),
            Arrays.asList(own, borrowed));
        List<NominatorGlobalSearch.Row> actors = NominatorGlobalSearch.search(catalog, b, "", false);
        check(
            NominatorBrowser.resourceLabel(actors.get(0), false)
                .equals("[角色] 同名 [引用]"),
            "actor reference label");
        check(
            NominatorBrowser.resourceLabel(actors.get(1), false)
                .equals("[角色组] 同名"),
            "actor group label");
        check(
            NominatorBrowser.sourceLabel(actors.get(0), false, catalog)
                .equals("来源：来源故事A"),
            "reference owner");
        check(
            NominatorBrowser.sourceLabel(actors.get(1), false, catalog)
                .isEmpty(),
            "ordinary owned source hidden");
        check(
            NominatorBrowser.sourceLabel(actors.get(1), true, catalog)
                .equals("所属：所属故事B"),
            "search owned scope");
        List<NominatorGlobalSearch.Row> items = NominatorGlobalSearch.search(catalog, b, "", true);
        check(
            NominatorBrowser.resourceLabel(items.get(0), false)
                .equals("[物品] 铜币 [引用]"),
            "item reference label");
        check(
            NominatorBrowser.resourceLabel(items.get(1), false)
                .equals("[物品组] 剑"),
            "item group label");
        List<NominatorGlobalSearch.Row> matches = NominatorGlobalSearch.search(catalog, a, "同名", false);
        check(matches.size() == 3, "all same-name scopes retained");
        check(
            matches.get(0).id.equals(matches.get(1).id) && !matches.get(0).source.getPackageId()
                .equals(matches.get(1).source.getPackageId()),
            "shared reference keeps independent scope identity");
        check(!matches.get(1).id.equals(matches.get(2).id), "distinct same-name identities retained");
    }

    private static void scrolling() {
        int height = RuntimeDirectoryVisuals.ROW_HEIGHT;
        SmoothScroll motion = new SmoothScroll();
        motion.bounds(37 * height - 97);
        for (int step = 0; step < 60; step++) {
            motion.wheel(-1);
            for (int frame = 0; frame < 5; frame++) {
                motion.advance(1.0 / 60);
                for (int y = 0; y < 97; y++) {
                    int row = (y + motion.pixelOffset()) / height;
                    int top = row * height - motion.pixelOffset();
                    check(y >= top && y < top + height, "drawn row and click agree throughout scroll");
                }
            }
        }
        SmoothScroll restored = new SmoothScroll();
        restored.bounds(37 * height - 97);
        restored.restore(motion);
        check(restored.pixelOffset() == motion.pixelOffset(), "resize/catalog restoration keeps scroll position");
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
