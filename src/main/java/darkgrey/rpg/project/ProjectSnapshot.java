package darkgrey.rpg.project;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.story.StoryDefinition;

public final class ProjectSnapshot {

    private final ProjectDefinition project;
    private final Map<String, ActorDefinition> actors;
    private final Map<String, DialogueDefinition> dialogues;
    private final Map<String, QuestDefinition> quests;
    private final Map<String, StoryDefinition> stories;
    private final CanonicalProjectContent canonicalContent;

    public ProjectSnapshot(ProjectDefinition project, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests,
        Map<String, StoryDefinition> stories) {
        this(project, actors, dialogues, quests, stories, CanonicalProjectContent.empty());
    }

    public ProjectSnapshot(ProjectDefinition project, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests,
        Map<String, StoryDefinition> stories, CanonicalProjectContent canonicalContent) {
        if (canonicalContent == null) throw new IllegalArgumentException("canonicalContent cannot be null.");
        this.project = project;
        this.actors = Collections.unmodifiableMap(new LinkedHashMap<String, ActorDefinition>(actors));
        this.dialogues = Collections.unmodifiableMap(new LinkedHashMap<String, DialogueDefinition>(dialogues));
        this.quests = Collections.unmodifiableMap(new LinkedHashMap<String, QuestDefinition>(quests));
        this.stories = Collections.unmodifiableMap(new LinkedHashMap<String, StoryDefinition>(stories));
        this.canonicalContent = canonicalContent;
    }

    public static ProjectSnapshot empty() {
        return new ProjectSnapshot(
            new ProjectDefinition(1, "unloaded", "Unloaded project"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap());
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

    public Map<String, DialogueDefinition> getDialogues() {
        return dialogues;
    }

    public DialogueDefinition getDialogue(String id) {
        return dialogues.get(id);
    }

    public Map<String, QuestDefinition> getQuests() {
        return quests;
    }

    public QuestDefinition getQuest(String id) {
        return quests.get(id);
    }

    public Map<String, StoryDefinition> getStories() {
        return stories;
    }

    public StoryDefinition getStory(String id) {
        return stories.get(id);
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
}
