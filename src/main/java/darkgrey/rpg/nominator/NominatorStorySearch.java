package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Set;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.StoryDefinition;

/** Read-only story browser used by both GUI implementations and probes. */
public final class NominatorStorySearch {

    private NominatorStorySearch() {}

    public static List<StoryChoice> stories(ProjectSnapshot snapshot, String query) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        String needle = normalize(query);
        List<StoryChoice> result = new ArrayList<StoryChoice>();
        for (StoryDefinition story : snapshot.getStories()
            .values()) {
            if (needle.isEmpty() || contains(story.getId(), needle)
                || contains(story.getTitle(), needle)
                || contains(story.getNotes(), needle)
                || tagsContain(story.getTags(), needle)) {
                result.add(new StoryChoice(story.getId(), story.getTitle(), story.getNotes(), story.getTags()));
            }
        }
        for (CanonicalGraphResource story : snapshot.getCanonicalStories()
            .values()) {
            if (snapshot.getStory(story.getId()) != null) continue;
            if (needle.isEmpty() || contains(story.getId(), needle) || contains(story.getDisplayName(), needle))
                result.add(new StoryChoice(story.getId(), story.getDisplayName()));
        }
        Collections.sort(result, StoryChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    /** Client-side search over the server snapshot; no repository access. */
    public static List<StoryChoice> stories(NominatorCatalog catalog, String query) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        String needle = normalize(query);
        List<StoryChoice> result = new ArrayList<StoryChoice>();
        for (NominatorCatalog.Story story : catalog.getStories())
            if (needle.isEmpty() || contains(story.getId(), needle)
                || contains(story.getTitle(), needle)
                || contains(story.getNotes(), needle)
                || tagsContain(story.getTags(), needle))
                result.add(new StoryChoice(story.getId(), story.getTitle(), story.getNotes(), story.getTags()));
        Collections.sort(result, StoryChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    public static List<ActorChoice> actors(ProjectSnapshot snapshot, String storyId, String query) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        String needle = normalize(query);
        List<ActorChoice> result = new ArrayList<ActorChoice>();
        for (ActorDefinition actor : snapshot.getActors()
            .values()) {
            if (storyId != null && !storyId.trim()
                .isEmpty() && !storyId.equals(actor.getHomeStoryId())) continue;
            if (needle.isEmpty() || contains(actor.getId(), needle)
                || contains(actor.getDisplayName(), needle)
                || tagsContain(actor.getTags(), needle)) {
                result.add(
                    new ActorChoice(
                        actor.getId(),
                        actor.getDisplayName(),
                        actor.getType(),
                        actor.getHomeStoryId(),
                        actor.getNotes(),
                        actor.getTags()));
            }
        }
        Collections.sort(result, ActorChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    public static List<ActorChoice> actors(NominatorCatalog catalog, String storyId, String query) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        String needle = normalize(query);
        List<ActorChoice> result = new ArrayList<ActorChoice>();
        for (NominatorCatalog.Actor actor : catalog.getActors()) {
            if (storyId != null && !storyId.trim()
                .isEmpty() && !storyId.equals(actor.getStoryId())) continue;
            if (needle.isEmpty() || contains(actor.getId(), needle)
                || contains(actor.getDisplayName(), needle)
                || tagsContain(actor.getTags(), needle))
                result.add(
                    new ActorChoice(
                        actor.getId(),
                        actor.getDisplayName(),
                        actor.getType(),
                        actor.getStoryId(),
                        actor.getNotes(),
                        actor.getTags()));
        }
        Collections.sort(result, ActorChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    /** Searches only the Actor IDs in one package's closure. */
    public static List<ActorChoice> actors(NominatorCatalog catalog, NominatorCatalog.PackageChoice packageChoice,
        String query) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        if (packageChoice == null) throw new IllegalArgumentException("Package choice is required.");
        String needle = normalize(query);
        Set<String> allowed = new java.util.HashSet<String>(packageChoice.getActorIds());
        List<ActorChoice> result = new ArrayList<ActorChoice>();
        for (NominatorCatalog.Actor actor : catalog.getActors()) {
            if (!allowed.contains(actor.getId())) continue;
            if (needle.isEmpty() || contains(actor.getId(), needle)
                || contains(actor.getDisplayName(), needle)
                || tagsContain(actor.getTags(), needle))
                result.add(
                    new ActorChoice(
                        actor.getId(),
                        actor.getDisplayName(),
                        actor.getType(),
                        actor.getStoryId(),
                        actor.getNotes(),
                        actor.getTags()));
        }
        Collections.sort(result, ActorChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    /**
     * Resolves one actor from the server-provided catalog. This intentionally
     * does not perform partial, case-insensitive, or display-name matching:
     * entity binding must be based on the exact Actor ID authored in Studio.
     */
    public static ActorChoice exactActor(NominatorCatalog catalog, String actorId) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        if (actorId == null) return null;
        String id = actorId.trim();
        if (id.isEmpty()) return null;
        for (NominatorCatalog.Actor actor : catalog.getActors()) {
            if (id.equals(actor.getId())) {
                return new ActorChoice(
                    actor.getId(),
                    actor.getDisplayName(),
                    actor.getType(),
                    actor.getStoryId(),
                    actor.getNotes(),
                    actor.getTags());
            }
        }
        return null;
    }

    public static List<ItemChoice> items(ProjectSnapshot snapshot, String query, boolean groups) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        String needle = normalize(query);
        List<ItemChoice> result = new ArrayList<ItemChoice>();
        for (ItemResourceDefinition item : (groups ? snapshot.getItemGroups()
            .values()
            : snapshot.getItems()
                .values())) {
            if (needle.isEmpty() || contains(item.getId(), needle)
                || contains(item.getDisplayName(), needle)
                || tagsContain(item.getTags(), needle))
                result.add(new ItemChoice(item.getId(), item.getDisplayName(), item.getTags(), groups));
        }
        Collections.sort(result, ItemChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    public static List<ItemChoice> items(NominatorCatalog catalog, String query, boolean groups) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        String needle = normalize(query);
        List<ItemChoice> result = new ArrayList<ItemChoice>();
        for (NominatorCatalog.Item item : groups ? catalog.getItemGroups() : catalog.getItems())
            if (needle.isEmpty() || contains(item.getId(), needle)
                || contains(item.getDisplayName(), needle)
                || tagsContain(item.getTags(), needle))
                result.add(new ItemChoice(item.getId(), item.getDisplayName(), item.getTags(), groups));
        Collections.sort(result, ItemChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    /** Searches only Item or Item Group IDs in one package's closure. */
    public static List<ItemChoice> items(NominatorCatalog catalog, NominatorCatalog.PackageChoice packageChoice,
        String query, boolean groups) {
        if (catalog == null) throw new IllegalArgumentException("Nominator catalog is required.");
        if (packageChoice == null) throw new IllegalArgumentException("Package choice is required.");
        String needle = normalize(query);
        Set<String> allowed = new java.util.HashSet<String>(
            groups ? packageChoice.getItemGroupIds() : packageChoice.getItemIds());
        List<ItemChoice> result = new ArrayList<ItemChoice>();
        for (NominatorCatalog.Item item : groups ? catalog.getItemGroups() : catalog.getItems()) {
            if (!allowed.contains(item.getId())) continue;
            if (needle.isEmpty() || contains(item.getId(), needle)
                || contains(item.getDisplayName(), needle)
                || tagsContain(item.getTags(), needle))
                result.add(new ItemChoice(item.getId(), item.getDisplayName(), item.getTags(), groups));
        }
        Collections.sort(result, ItemChoice.ORDER);
        return Collections.unmodifiableList(result);
    }

    private static boolean tagsContain(List<String> tags, String needle) {
        for (String tag : tags) if (contains(tag, needle)) return true;
        return false;
    }

    private static boolean contains(String value, String needle) {
        return value != null && value.toLowerCase()
            .contains(needle);
    }

    private static String normalize(String value) {
        return value == null ? ""
            : value.trim()
                .toLowerCase();
    }

    public static final class StoryChoice {

        private static final Comparator<StoryChoice> ORDER = new Comparator<StoryChoice>() {

            @Override
            public int compare(StoryChoice left, StoryChoice right) {
                return left.id.compareTo(right.id);
            }
        };
        private final String id;
        private final String title;
        private final String notes;
        private final List<String> tags;

        public StoryChoice(String id, String title) {
            this(id, title, "", Collections.<String>emptyList());
        }

        public StoryChoice(String id, String title, String notes, List<String> tags) {
            this.id = id;
            this.title = title;
            this.notes = notes;
            this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
        }

        public String getId() {
            return id;
        }

        public String getTitle() {
            return title;
        }

        public String getNotes() {
            return notes;
        }

        public List<String> getTags() {
            return tags;
        }
    }

    public static final class ActorChoice {

        private static final Comparator<ActorChoice> ORDER = new Comparator<ActorChoice>() {

            @Override
            public int compare(ActorChoice left, ActorChoice right) {
                return left.id.compareTo(right.id);
            }
        };
        private final String id;
        private final String displayName;
        private final String type;
        private final String storyId;
        private final String notes;
        private final List<String> tags;

        public ActorChoice(String id, String displayName, String type) {
            this(id, displayName, type, null, "", Collections.<String>emptyList());
        }

        public ActorChoice(String id, String displayName, String type, String notes, List<String> tags) {
            this(id, displayName, type, null, notes, tags);
        }

        public ActorChoice(String id, String displayName, String type, String storyId, String notes,
            List<String> tags) {
            this.id = id;
            this.displayName = displayName;
            this.type = type;
            this.storyId = storyId;
            this.notes = notes;
            this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
        }

        public String getId() {
            return id;
        }

        public String getDisplayName() {
            return displayName;
        }

        public String getType() {
            return type;
        }

        public String getStoryId() {
            return storyId;
        }

        public String getNotes() {
            return notes;
        }

        public List<String> getTags() {
            return tags;
        }
    }

    public static final class ItemChoice {

        private static final Comparator<ItemChoice> ORDER = new Comparator<ItemChoice>() {

            @Override
            public int compare(ItemChoice left, ItemChoice right) {
                return left.id.compareTo(right.id);
            }
        };
        private final String id;
        private final String displayName;
        private final List<String> tags;
        private final boolean group;

        public ItemChoice(String id, String displayName, List<String> tags, boolean group) {
            this.id = id;
            this.displayName = displayName;
            this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
            this.group = group;
        }

        public String getId() {
            return id;
        }

        public String getDisplayName() {
            return displayName;
        }

        public List<String> getTags() {
            return tags;
        }

        public boolean isGroup() {
            return group;
        }
    }
}
