package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.List;

import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.NominatorStorySearch;

/** Package-scoped presentation results; shared IDs deliberately remain separate rows. */
public final class NominatorGlobalSearch {

    private NominatorGlobalSearch() {}

    public static final class Row {

        public final NominatorCatalog.PackageChoice source;
        public final String id, name, type;

        public Row(NominatorCatalog.PackageChoice source, String id, String name, String type) {
            this.source = source;
            this.id = id;
            this.name = name;
            this.type = type;
        }
    }

    public static List<Row> search(NominatorCatalog catalog, String selectedPackage, String query, boolean items) {
        List<Row> rows = new ArrayList<Row>();
        boolean global = query != null && !query.trim()
            .isEmpty();
        for (NominatorCatalog.PackageChoice source : catalog.getPackageChoices()) {
            if (!global && !source.getPackageId()
                .equals(selectedPackage)) continue;
            String scopeQuery = query;
            if (global && (source.getDisplayName()
                .toLowerCase(java.util.Locale.ROOT)
                .contains(query.toLowerCase(java.util.Locale.ROOT))
                || source.getContainerName()
                    .toLowerCase(java.util.Locale.ROOT)
                    .contains(query.toLowerCase(java.util.Locale.ROOT))))
                scopeQuery = "";
            if (items) {
                for (boolean group : new boolean[] { false, true })
                    for (NominatorStorySearch.ItemChoice item : NominatorStorySearch
                        .items(catalog, source, scopeQuery, group))
                        rows.add(new Row(source, item.getId(), item.getDisplayName(), group ? "Item Group" : "Item"));
            } else {
                for (NominatorStorySearch.ActorChoice actor : NominatorStorySearch
                    .actors(catalog, source, scopeQuery)) {
                    if ("individual".equals(actor.getType()) || "collective".equals(actor.getType())) rows.add(
                        new Row(
                            source,
                            actor.getId(),
                            actor.getDisplayName(),
                            "individual".equals(actor.getType()) ? "NPC" : "Group"));
                }
            }
        }
        return rows;
    }
}
