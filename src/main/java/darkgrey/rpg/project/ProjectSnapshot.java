package darkgrey.rpg.project;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;

/** Immutable snapshot of the single current author/runtime content model. */
public final class ProjectSnapshot {

    private final ProjectDefinition project;
    private final Map<String, ActorDefinition> actors;
    private final Map<String, ItemResourceDefinition> items;
    private final Map<String, ItemResourceDefinition> itemGroups;
    private final CanonicalProjectContent canonicalContent;

    public ProjectSnapshot(ProjectDefinition project, Map<String, ActorDefinition> actors,
        Map<String, ItemResourceDefinition> items, Map<String, ItemResourceDefinition> itemGroups,
        CanonicalProjectContent canonicalContent) {
        this.project = Objects.requireNonNull(project, "project");
        this.actors = Collections.unmodifiableMap(new LinkedHashMap<String, ActorDefinition>(actors));
        this.items = Collections.unmodifiableMap(new LinkedHashMap<String, ItemResourceDefinition>(items));
        this.itemGroups = Collections.unmodifiableMap(new LinkedHashMap<String, ItemResourceDefinition>(itemGroups));
        this.canonicalContent = Objects.requireNonNull(canonicalContent, "canonicalContent");
    }

    public static ProjectSnapshot empty() {
        return new ProjectSnapshot(
            new ProjectDefinition(1, "unloaded", "Unloaded project"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            CanonicalProjectContent.empty());
    }

    public ProjectDefinition getProject() {
        return project;
    }

    public Map<String, ActorDefinition> getActors() {
        return actors;
    }

    public ActorDefinition getActor(String id) {
        return actors.get(id);
    }

    public Map<String, ItemResourceDefinition> getItems() {
        return items;
    }

    public ItemResourceDefinition getItem(String id) {
        return items.get(id);
    }

    public Map<String, ItemResourceDefinition> getItemGroups() {
        return itemGroups;
    }

    public ItemResourceDefinition getItemGroup(String id) {
        return itemGroups.get(id);
    }

    public boolean containsStory(String id) {
        return id != null && !id.trim()
            .isEmpty() && canonicalContent.getStory(id.trim()) != null;
    }

    public CanonicalProjectContent getCanonicalContent() {
        return canonicalContent;
    }

    public Map<String, CanonicalGraphResource> getCanonicalStories() {
        return canonicalContent.getStories();
    }

    public CanonicalGraphResource getCanonicalStory(String id) {
        return canonicalContent.getStory(id);
    }

    public Map<String, CanonicalGraphResource> getCanonicalSessions() {
        return canonicalContent.getSessions();
    }

    public CanonicalGraphResource getCanonicalSession(String id) {
        return canonicalContent.getSession(id);
    }

    public Map<String, CanonicalGraphResource> getCanonicalTasks() {
        return canonicalContent.getTasks();
    }

    public CanonicalGraphResource getCanonicalTask(String id) {
        return canonicalContent.getTask(id);
    }

    public Map<String, CanonicalStoryMembership> getCanonicalStoryMemberships() {
        return canonicalContent.getMemberships();
    }

    public CanonicalStoryMembership getCanonicalStoryMembership(String id) {
        return canonicalContent.getMembership(id);
    }

    public CanonicalStoryLogicGraph getCanonicalStoryLogicGraph() {
        return canonicalContent.getStoryLogicGraph();
    }

    public List<CanonicalStoryLogicConnection> getCanonicalStoryLogicConnections() {
        return canonicalContent.getStoryLogicConnections();
    }
}
