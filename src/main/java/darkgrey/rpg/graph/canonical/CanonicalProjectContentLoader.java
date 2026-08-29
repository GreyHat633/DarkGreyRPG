package darkgrey.rpg.graph.canonical;

import java.io.File;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Strict, read-only loader for the complete canonical project-content tree. */
public final class CanonicalProjectContentLoader {

    private static final String RESOURCES = "resources";
    private static final String CANONICAL = "canonical";

    private final CanonicalGraphResourceLoader resourceLoader;
    private final CanonicalStoryMembershipLoader membershipLoader;

    public CanonicalProjectContentLoader() {
        this(new CanonicalGraphResourceLoader(), new CanonicalStoryMembershipLoader());
    }

    CanonicalProjectContentLoader(CanonicalGraphResourceLoader resourceLoader,
        CanonicalStoryMembershipLoader membershipLoader) {
        if (resourceLoader == null) throw new IllegalArgumentException("resourceLoader cannot be null.");
        if (membershipLoader == null) throw new IllegalArgumentException("membershipLoader cannot be null.");
        this.resourceLoader = resourceLoader;
        this.membershipLoader = membershipLoader;
    }

    public CanonicalProjectContent load(File projectDirectory, Set<String> actorIds)
        throws CanonicalProjectContentException {
        return load(projectDirectory == null ? null : projectDirectory.toPath(), actorIds);
    }

    public CanonicalProjectContent load(Path projectDirectory, Set<String> actorIds)
        throws CanonicalProjectContentException {
        if (projectDirectory == null) throw CanonicalProjectContentException.failure(
            "project.content.project.directory.required",
            "Canonical project content requires a project directory.");

        Path canonicalRoot = projectDirectory.resolve(RESOURCES)
            .resolve(CANONICAL);
        if (!Files.exists(canonicalRoot)) return CanonicalProjectContent.empty();
        if (!Files.isDirectory(canonicalRoot)) throw CanonicalProjectContentException.failure(
            "project.content.root.invalid",
            "Canonical project content root is not a directory: " + canonicalRoot);
        if (actorIds == null) throw CanonicalProjectContentException.failure(
            "project.content.actor_ids.required",
            "Already-loaded Actor IDs are required when canonical content exists.");

        Path storiesDirectory = requireDirectory(canonicalRoot, "stories");
        Path sessionsDirectory = requireDirectory(canonicalRoot, "sessions");
        Path tasksDirectory = requireDirectory(canonicalRoot, "tasks");
        Path membershipsDirectory = requireDirectory(canonicalRoot, "memberships");

        List<CanonicalGraphResource> storyValues = loadStories(storiesDirectory);
        List<CanonicalGraphResource> sessionValues = loadSessions(sessionsDirectory);
        List<CanonicalGraphResource> taskValues = loadTasks(tasksDirectory);
        List<CanonicalStoryMembership> membershipValues = loadMemberships(membershipsDirectory);

        Map<String, CanonicalGraphResource> stories = resources(storyValues);
        Map<String, CanonicalGraphResource> sessions = resources(sessionValues);
        Map<String, CanonicalGraphResource> tasks = resources(taskValues);
        Map<String, CanonicalStoryMembership> memberships = memberships(membershipValues);
        validate(stories, sessions, tasks, memberships, actorIds);
        return new CanonicalProjectContent(stories, sessions, tasks, memberships);
    }

    private List<CanonicalGraphResource> loadStories(Path directory) {
        try {
            return resourceLoader.loadStories(directory);
        } catch (CanonicalGraphResourceException exception) {
            throw wrapped("project.content.stories.load", directory, exception);
        }
    }

    private List<CanonicalGraphResource> loadSessions(Path directory) {
        try {
            return resourceLoader.loadSessions(directory);
        } catch (CanonicalGraphResourceException exception) {
            throw wrapped("project.content.sessions.load", directory, exception);
        }
    }

    private List<CanonicalGraphResource> loadTasks(Path directory) {
        try {
            return resourceLoader.loadTasks(directory);
        } catch (CanonicalGraphResourceException exception) {
            throw wrapped("project.content.tasks.load", directory, exception);
        }
    }

    private List<CanonicalStoryMembership> loadMemberships(Path directory) {
        try {
            return membershipLoader.loadDirectory(directory);
        } catch (CanonicalStoryMembershipException exception) {
            throw wrapped("project.content.memberships.load", directory, exception);
        }
    }

    private static Path requireDirectory(Path root, String name) {
        Path directory = root.resolve(name);
        if (!Files.isDirectory(directory)) throw CanonicalProjectContentException.failure(
            "project.content.directory.missing",
            "Required canonical project-content directory is missing or not a directory: " + directory);
        return directory;
    }

    private static CanonicalProjectContentException wrapped(String code, Path directory, Throwable cause) {
        return new CanonicalProjectContentException(
            code,
            "Could not load canonical project-content directory '" + directory + "'.",
            cause);
    }

    private static Map<String, CanonicalGraphResource> resources(List<CanonicalGraphResource> values) {
        Map<String, CanonicalGraphResource> result = new LinkedHashMap<String, CanonicalGraphResource>();
        for (CanonicalGraphResource value : values) result.put(value.getId(), value);
        return result;
    }

    private static Map<String, CanonicalStoryMembership> memberships(List<CanonicalStoryMembership> values) {
        Map<String, CanonicalStoryMembership> result = new LinkedHashMap<String, CanonicalStoryMembership>();
        for (CanonicalStoryMembership value : values) result.put(value.getStoryId(), value);
        return result;
    }

    private static void validate(Map<String, CanonicalGraphResource> stories,
        Map<String, CanonicalGraphResource> sessions, Map<String, CanonicalGraphResource> tasks,
        Map<String, CanonicalStoryMembership> memberships, Set<String> actorIds) {
        for (String storyId : stories.keySet())
            if (!memberships.containsKey(storyId)) throw CanonicalProjectContentException
                .failure("project.content.membership.missing", "Canonical Story has no same-ID membership: " + storyId);
        for (String storyId : memberships.keySet())
            if (!stories.containsKey(storyId)) throw CanonicalProjectContentException
                .failure("project.content.story.missing", "Canonical membership has no same-ID Story: " + storyId);

        Set<String> knownActors = new HashSet<String>(actorIds);
        Set<String> ownedActors = new HashSet<String>();
        Set<String> ownedSessions = new HashSet<String>();
        Set<String> ownedTasks = new HashSet<String>();
        for (CanonicalStoryMembership membership : memberships.values()) {
            checkActors(membership, knownActors, ownedActors);
            checkResources(
                membership.getOwnedResources()
                    .getSessions(),
                sessions,
                ownedSessions,
                "session");
            checkResources(
                membership.getReferencedResources()
                    .getSessions(),
                sessions,
                null,
                "session");
            checkResources(
                membership.getOwnedResources()
                    .getTasks(),
                tasks,
                ownedTasks,
                "task");
            checkResources(
                membership.getReferencedResources()
                    .getTasks(),
                tasks,
                null,
                "task");
        }
    }

    private static void checkActors(CanonicalStoryMembership membership, Set<String> actorIds,
        Set<String> ownedActors) {
        for (String id : membership.getOwnedResources()
            .getActors()) {
            requireActor(actorIds, id);
            if (!ownedActors.add(id)) throw CanonicalProjectContentException.failure(
                "project.content.actor.ownership.duplicate",
                "Actor is owned by more than one canonical Story: " + id);
        }
        for (String id : membership.getReferencedResources()
            .getActors()) requireActor(actorIds, id);
    }

    private static void requireActor(Set<String> actorIds, String id) {
        if (!actorIds.contains(id)) throw CanonicalProjectContentException
            .failure("project.content.actor.missing", "Canonical membership references an unknown Actor: " + id);
    }

    private static void checkResources(List<String> ids, Map<String, CanonicalGraphResource> resources,
        Set<String> owned, String kind) {
        for (String id : ids) {
            if (!resources.containsKey(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".missing",
                "Canonical membership references an unknown " + kind + ": " + id);
            if (owned != null && !owned.add(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".ownership.duplicate",
                kind + " is owned by more than one canonical Story: " + id);
        }
    }
}
