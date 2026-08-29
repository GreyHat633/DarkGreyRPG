package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.StoryDefinition;

/** Bounded, server-produced data used by the nominator screens. */
public final class NominatorCatalog {

    public static final int MAX_ENTRIES = 512;
    public static final int MAX_TAGS = 32;

    private final List<Story> stories;
    private final List<Actor> actors;
    private final List<Item> items;
    private final List<Item> itemGroups;

    public NominatorCatalog(List<Story> stories, List<Actor> actors, List<Item> items, List<Item> itemGroups) {
        this.stories = bounded(stories);
        this.actors = bounded(actors);
        this.items = bounded(items);
        this.itemGroups = bounded(itemGroups);
    }

    public static NominatorCatalog from(ProjectSnapshot snapshot) {
        if (snapshot == null) throw new IllegalArgumentException("Project snapshot is required.");
        List<Story> stories = new ArrayList<Story>();
        for (StoryDefinition value : snapshot.getStories()
            .values()) stories.add(new Story(value.getId(), value.getTitle(), value.getNotes(), value.getTags()));
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
        return new NominatorCatalog(stories, actors, items(snapshot.getItems()), items(snapshot.getItemGroups()));
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

    private static List<String> tags(List<String> source) {
        if (source == null) return Collections.emptyList();
        if (source.size() > MAX_TAGS) throw new IllegalArgumentException("Nominator catalog entry has too many tags.");
        return Collections.unmodifiableList(new ArrayList<String>(source));
    }
}
