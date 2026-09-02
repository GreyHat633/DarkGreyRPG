package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.story.StoryDefinition;

/** Bounded, server-produced data used by the nominator screens. */
public final class NominatorCatalog {

    public static final int MAX_ENTRIES = 512;
    public static final int MAX_TAGS = 32;

    private final List<Story> stories;
    private final List<Actor> actors;
    private final List<Item> items;
    private final List<Item> itemGroups;
    private final List<PackageChoice> packageChoices;

    public NominatorCatalog(List<Story> stories, List<Actor> actors, List<Item> items, List<Item> itemGroups) {
        this(stories, actors, items, itemGroups, Collections.<PackageChoice>emptyList());
    }

    public NominatorCatalog(List<Story> stories, List<Actor> actors, List<Item> items, List<Item> itemGroups,
        List<PackageChoice> packageChoices) {
        this.stories = bounded(stories);
        this.actors = bounded(actors);
        this.items = bounded(items);
        this.itemGroups = bounded(itemGroups);
        this.packageChoices = bounded(packageChoices);
    }

    public static NominatorCatalog from(ProjectSnapshot snapshot) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        Map<String, Story> storiesById = new java.util.LinkedHashMap<String, Story>();
        for (StoryDefinition value : snapshot.getStories()
            .values())
            storiesById
                .put(value.getId(), new Story(value.getId(), value.getTitle(), value.getNotes(), value.getTags()));
        for (CanonicalGraphResource value : snapshot.getCanonicalStories()
            .values())
            if (!storiesById.containsKey(value.getId())) storiesById.put(
                value.getId(),
                new Story(value.getId(), value.getDisplayName(), "", Collections.<String>emptyList()));
        List<Actor> actors = new ArrayList<Actor>();
        for (ActorDefinition value : snapshot.getActors()
            .values())
            actors.add(
                new Actor(
                    value.getId(),
                    value.getDisplayName(),
                    value.getType(),
                    value.getHomeStoryId(),
                    value.getNotes(),
                    value.getTags()));
        return new NominatorCatalog(
            new ArrayList<Story>(storiesById.values()),
            actors,
            items(snapshot.getItems()),
            items(snapshot.getItemGroups()));
    }

    /** Builds the global catalog and package closure choices from accepted packages. */
    public static NominatorCatalog from(Map<String, LoadedStoryPackage> packages, ProjectSnapshot snapshot) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        if (packages == null) throw new IllegalArgumentException("Loaded Story Packages are required.");
        NominatorCatalog global = from(snapshot);
        List<PackageChoice> choices = new ArrayList<PackageChoice>();
        for (LoadedStoryPackage value : packages.values()) {
            if (value == null) throw new IllegalArgumentException("Loaded Story Package cannot be null.");
            choices.add(packageChoice(value, snapshot));
        }
        return new NominatorCatalog(
            global.getStories(),
            global.getActors(),
            global.getItems(),
            global.getItemGroups(),
            choices);
    }

    /** Preferred argument order for callers starting from the authoritative snapshot. */
    public static NominatorCatalog from(ProjectSnapshot snapshot, Map<String, LoadedStoryPackage> packages) {
        return from(packages, snapshot);
    }

    /** Convenience overload for callers holding the accepted package values. */
    public static NominatorCatalog from(Iterable<LoadedStoryPackage> packages, ProjectSnapshot snapshot) {
        if (packages == null) throw new IllegalArgumentException("Loaded Story Packages are required.");
        Map<String, LoadedStoryPackage> indexed = new java.util.LinkedHashMap<String, LoadedStoryPackage>();
        for (LoadedStoryPackage value : packages) {
            if (value == null) throw new IllegalArgumentException("Loaded Story Package cannot be null.");
            indexed.put(value.getPackageId(), value);
        }
        return from(indexed, snapshot);
    }

    private static PackageChoice packageChoice(LoadedStoryPackage value, ProjectSnapshot merged) {
        ProjectSnapshot packageSnapshot = value.getSnapshot();
        String storyId = value.getStoryId();
        CanonicalGraphResource canonical = packageSnapshot.getCanonicalStory(storyId);
        StoryDefinition legacy = packageSnapshot.getStory(storyId);
        String displayName = canonical != null ? canonical.getDisplayName()
            : legacy == null ? storyId : legacy.getTitle();
        Set<String> actorIds = new LinkedHashSet<String>();
        Set<String> itemIds = new LinkedHashSet<String>();
        Set<String> itemGroupIds = new LinkedHashSet<String>();
        CanonicalStoryMembership membership = packageSnapshot.getCanonicalStoryMembership(storyId);
        if (membership != null) {
            addMembership(membership.getOwnedResources(), actorIds, itemIds, itemGroupIds);
            addMembership(membership.getReferencedResources(), actorIds, itemIds, itemGroupIds);
        } else {
            actorIds.addAll(
                packageSnapshot.getActors()
                    .keySet());
            itemIds.addAll(
                packageSnapshot.getItems()
                    .keySet());
            itemGroupIds.addAll(
                packageSnapshot.getItemGroups()
                    .keySet());
        }
        // A validated package closure must resolve in the authoritative merge.
        requirePresent(
            actorIds,
            merged.getActors()
                .keySet(),
            "Actor",
            value.getPackageId());
        requirePresent(
            itemIds,
            merged.getItems()
                .keySet(),
            "Item",
            value.getPackageId());
        requirePresent(
            itemGroupIds,
            merged.getItemGroups()
                .keySet(),
            "Item Group",
            value.getPackageId());
        return new PackageChoice(
            value.getPackageId(),
            storyId,
            displayName,
            new ArrayList<String>(actorIds),
            new ArrayList<String>(itemIds),
            new ArrayList<String>(itemGroupIds));
    }

    private static void addMembership(darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet values,
        Set<String> actors, Set<String> items, Set<String> itemGroups) {
        actors.addAll(values.getActors());
        items.addAll(values.getItems());
        itemGroups.addAll(values.getItemGroups());
    }

    private static void requirePresent(Set<String> ids, Set<String> available, String type, String packageId) {
        for (String id : ids) if (!available.contains(id)) throw new IllegalArgumentException(
            "Package '" + packageId + "' closure references missing " + type + " '" + id + "'.");
    }

    private static List<Item> items(java.util.Map<String, ItemResourceDefinition> source) {
        List<Item> result = new ArrayList<Item>();
        for (ItemResourceDefinition value : source.values())
            result.add(new Item(value.getId(), value.getDisplayName(), value.getTags()));
        return result;
    }

    private static <T> List<T> bounded(List<T> source) {
        if (source == null) return Collections.emptyList();
        if (source.size() > MAX_ENTRIES) throw new IllegalArgumentException("Nominator catalog is too large.");
        return Collections.unmodifiableList(new ArrayList<T>(source));
    }

    public List<Story> getStories() {
        return stories;
    }

    public List<Actor> getActors() {
        return actors;
    }

    public List<Item> getItems() {
        return items;
    }

    public List<Item> getItemGroups() {
        return itemGroups;
    }

    public List<PackageChoice> getPackageChoices() {
        return packageChoices;
    }

    public PackageChoice getPackageChoice(String packageId) {
        if (packageId == null) return null;
        for (PackageChoice value : packageChoices) if (packageId.equals(value.getPackageId())) return value;
        return null;
    }

    public static final class Story {

        private final String id, title, notes;
        private final List<String> tags;

        public Story(String id, String title, String notes, List<String> tags) {
            this.id = id;
            this.title = title;
            this.notes = notes;
            this.tags = tags(tags);
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

    public static final class Actor {

        private final String id, displayName, type, storyId, notes;
        private final List<String> tags;

        public Actor(String id, String displayName, String type, String storyId, String notes, List<String> tags) {
            this.id = id;
            this.displayName = displayName;
            this.type = type;
            this.storyId = storyId;
            this.notes = notes;
            this.tags = tags(tags);
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

    public static final class Item {

        private final String id, displayName;
        private final List<String> tags;

        public Item(String id, String displayName, List<String> tags) {
            this.id = id;
            this.displayName = displayName;
            this.tags = tags(tags);
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
    }

    /** One accepted package and its exact Story resource closure. */
    public static final class PackageChoice {

        private final String packageId;
        private final String storyId;
        private final String displayName;
        private final List<String> actorIds;
        private final List<String> itemIds;
        private final List<String> itemGroupIds;

        public PackageChoice(String packageId, String storyId, String displayName, List<String> actorIds,
            List<String> itemIds, List<String> itemGroupIds) {
            this.packageId = requiredText(packageId, "packageId");
            this.storyId = requiredText(storyId, "storyId");
            this.displayName = requiredText(displayName, "displayName");
            this.actorIds = boundedIds(actorIds);
            this.itemIds = boundedIds(itemIds);
            this.itemGroupIds = boundedIds(itemGroupIds);
        }

        public String getPackageId() {
            return packageId;
        }

        public String getStoryId() {
            return storyId;
        }

        public String getDisplayName() {
            return displayName;
        }

        public String getStoryDisplayName() {
            return displayName;
        }

        public String getCanonicalDisplayName() {
            return displayName;
        }

        public String getStoryTitle() {
            return displayName;
        }

        public List<String> getActorIds() {
            return actorIds;
        }

        public List<String> getItemIds() {
            return itemIds;
        }

        public List<String> getItemGroupIds() {
            return itemGroupIds;
        }

        public List<String> getActors() {
            return actorIds;
        }

        public List<String> getItems() {
            return itemIds;
        }

        public List<String> getItemGroups() {
            return itemGroupIds;
        }

        public boolean containsActor(String actorId) {
            return actorId != null && actorIds.contains(actorId.trim());
        }

        public boolean containsItem(String itemId) {
            return itemId != null && itemIds.contains(itemId.trim());
        }

        public boolean containsItemGroup(String itemGroupId) {
            return itemGroupId != null && itemGroupIds.contains(itemGroupId.trim());
        }

        private static List<String> boundedIds(List<String> source) {
            if (source == null) return Collections.emptyList();
            if (source.size() > MAX_ENTRIES) throw new IllegalArgumentException("Package closure is too large.");
            Set<String> unique = new LinkedHashSet<String>();
            for (String value : source) {
                String id = requiredText(value, "closureId");
                if (!unique.add(id)) throw new IllegalArgumentException("Package closure contains a duplicate ID.");
            }
            return Collections.unmodifiableList(new ArrayList<String>(unique));
        }
    }

    private static String requiredText(String value, String name) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(name + " is required.");
        return value.trim();
    }

    private static List<String> tags(List<String> source) {
        if (source == null) return Collections.emptyList();
        if (source.size() > MAX_TAGS) throw new IllegalArgumentException("Nominator catalog entry has too many tags.");
        return Collections.unmodifiableList(new ArrayList<String>(source));
    }
}
