package darkgrey.rpg.graph.canonical;

import java.io.File;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.identity.DgrResourceId;

/** Strict, read-only loader for the complete canonical project-content tree. */
public final class CanonicalProjectContentLoader {

    private static final String RESOURCES = "resources";
    private static final String CANONICAL = "canonical";

    private final CanonicalGraphResourceLoader resourceLoader;
    private final CanonicalStoryMembershipLoader membershipLoader;
    private final CanonicalStoryLogicGraphLoader storyLogicGraphLoader;

    public CanonicalProjectContentLoader() {
        this(
            new CanonicalGraphResourceLoader(),
            new CanonicalStoryMembershipLoader(),
            new CanonicalStoryLogicGraphLoader());
    }

    CanonicalProjectContentLoader(CanonicalGraphResourceLoader resourceLoader,
        CanonicalStoryMembershipLoader membershipLoader) {
        this(resourceLoader, membershipLoader, new CanonicalStoryLogicGraphLoader());
    }

    CanonicalProjectContentLoader(CanonicalGraphResourceLoader resourceLoader,
        CanonicalStoryMembershipLoader membershipLoader, CanonicalStoryLogicGraphLoader storyLogicGraphLoader) {
        if (resourceLoader == null) throw new IllegalArgumentException("resourceLoader cannot be null.");
        if (membershipLoader == null) throw new IllegalArgumentException("membershipLoader cannot be null.");
        if (storyLogicGraphLoader == null) throw new IllegalArgumentException("storyLogicGraphLoader cannot be null.");
        this.resourceLoader = resourceLoader;
        this.membershipLoader = membershipLoader;
        this.storyLogicGraphLoader = storyLogicGraphLoader;
    }

    public CanonicalProjectContent load(File projectDirectory, Set<String> actorIds)
        throws CanonicalProjectContentException {
        return load(projectDirectory == null ? null : projectDirectory.toPath(), actorIds);
    }

    public CanonicalProjectContent load(Path projectDirectory, Set<String> actorIds)
        throws CanonicalProjectContentException {
        return load(projectDirectory, actorIds, new HashSet<String>(), new HashSet<String>());
    }

    public CanonicalProjectContent load(File projectDirectory, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds) throws CanonicalProjectContentException {
        return load(projectDirectory == null ? null : projectDirectory.toPath(), actorIds, itemIds, itemGroupIds);
    }

    public CanonicalProjectContent load(Path projectDirectory, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds) throws CanonicalProjectContentException {
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
        if (itemIds == null || itemGroupIds == null) throw CanonicalProjectContentException.failure(
            "project.content.item_ids.required",
            "Already-loaded Item and Item Group IDs are required when canonical content exists.");

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
        validate(stories, sessions, tasks, memberships, actorIds, itemIds, itemGroupIds, false, true);
        CanonicalStoryLogicGraph storyLogicGraph = loadStoryLogicGraph(canonicalRoot, stories);
        return new CanonicalProjectContent(stories, sessions, tasks, memberships, storyLogicGraph);
    }

    /** Validates detached DGRS resources without materializing a project directory. */
    public CanonicalProjectContent loadPackageContent(Map<String, CanonicalGraphResource> stories,
        Map<String, CanonicalGraphResource> sessions, Map<String, CanonicalGraphResource> tasks,
        Map<String, CanonicalStoryMembership> memberships, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds) {
        if (stories == null || sessions == null || tasks == null || memberships == null)
            throw CanonicalProjectContentException.failure(
                "project.content.package.resources.required",
                "Detached canonical package resources are required.");
        if (actorIds == null || itemIds == null || itemGroupIds == null) throw CanonicalProjectContentException.failure(
            "project.content.package.identities.required",
            "Detached package Actor, Item, and Item Group IDs are required.");
        validate(stories, sessions, tasks, memberships, actorIds, itemIds, itemGroupIds, false, true);
        return new CanonicalProjectContent(stories, sessions, tasks, memberships, CanonicalStoryLogicGraph.empty());
    }

    /**
     * Validates one DGRS package before the installed package set is available.
     * Owned resources must be present; a missing referenced resource is deferred only
     * when its ID is a valid full ID. Typed graph references still have to be declared
     * by the package Story membership.
     */
    public CanonicalProjectContent loadPackageContentPartial(Map<String, CanonicalGraphResource> stories,
        Map<String, CanonicalGraphResource> sessions, Map<String, CanonicalGraphResource> tasks,
        Map<String, CanonicalStoryMembership> memberships, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds) {
        if (stories == null || sessions == null || tasks == null || memberships == null)
            throw CanonicalProjectContentException.failure(
                "project.content.package.resources.required",
                "Detached canonical package resources are required.");
        if (actorIds == null || itemIds == null || itemGroupIds == null) throw CanonicalProjectContentException.failure(
            "project.content.package.identities.required",
            "Detached package Actor, Item, and Item Group IDs are required.");
        validate(stories, sessions, tasks, memberships, actorIds, itemIds, itemGroupIds, true, false);
        return new CanonicalProjectContent(stories, sessions, tasks, memberships, CanonicalStoryLogicGraph.empty());
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

    private CanonicalStoryLogicGraph loadStoryLogicGraph(Path canonicalRoot,
        Map<String, CanonicalGraphResource> stories) {
        Path file = canonicalRoot.resolve("story_logic_graph.json");
        if (!Files.exists(file)) return CanonicalStoryLogicGraph.empty();
        try {
            return storyLogicGraphLoader.load(file, stories);
        } catch (CanonicalGraphResourceException exception) {
            throw wrapped("project.content.story_logic_graph.load", file, exception);
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
        Map<String, CanonicalStoryMembership> memberships, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds, boolean allowUnresolvedExternal, boolean resolveStoryTargets) {
        checkKinds(stories, CanonicalGraphResourceKind.STORY, "story");
        checkKinds(sessions, CanonicalGraphResourceKind.SESSION, "session");
        checkKinds(tasks, CanonicalGraphResourceKind.TASK, "task");
        for (Map.Entry<String, CanonicalStoryMembership> entry : memberships.entrySet()) {
            CanonicalStoryMembership value = entry.getValue();
            if (value == null || !entry.getKey()
                .equals(value.getStoryId()) || !DgrResourceId.isCompatibleId(entry.getKey()))
                throw CanonicalProjectContentException.failure(
                    "project.content.membership.id.mismatch",
                    "Canonical membership map key does not match a valid Story ID: " + entry.getKey());
        }
        for (String storyId : stories.keySet())
            if (!memberships.containsKey(storyId)) throw CanonicalProjectContentException
                .failure("project.content.membership.missing", "Canonical Story has no same-ID membership: " + storyId);
        for (String storyId : memberships.keySet())
            if (!stories.containsKey(storyId)) throw CanonicalProjectContentException
                .failure("project.content.story.missing", "Canonical membership has no same-ID Story: " + storyId);

        Set<String> knownActors = new HashSet<String>(actorIds);
        Set<String> ownedActors = new HashSet<String>();
        Set<String> ownedItems = new HashSet<String>();
        Set<String> ownedItemGroups = new HashSet<String>();
        Set<String> ownedSessions = new HashSet<String>();
        Set<String> ownedTasks = new HashSet<String>();
        for (CanonicalStoryMembership membership : memberships.values()) {
            checkTypedKinds(membership, actorIds, itemIds, itemGroupIds, sessions.keySet(), tasks.keySet());
            checkActors(membership, knownActors, ownedActors, allowUnresolvedExternal);
            checkIdentityResources(
                membership.getOwnedResources()
                    .getItems(),
                itemIds,
                ownedItems,
                "item",
                false);
            checkIdentityResources(
                membership.getReferencedResources()
                    .getItems(),
                itemIds,
                null,
                "item",
                allowUnresolvedExternal);
            checkIdentityResources(
                membership.getOwnedResources()
                    .getItemGroups(),
                itemGroupIds,
                ownedItemGroups,
                "item_group",
                false);
            checkIdentityResources(
                membership.getReferencedResources()
                    .getItemGroups(),
                itemGroupIds,
                null,
                "item_group",
                allowUnresolvedExternal);
            checkResources(
                membership.getOwnedResources()
                    .getSessions(),
                sessions,
                ownedSessions,
                "session",
                false);
            checkResources(
                membership.getReferencedResources()
                    .getSessions(),
                sessions,
                null,
                "session",
                allowUnresolvedExternal);
            checkResources(
                membership.getOwnedResources()
                    .getTasks(),
                tasks,
                ownedTasks,
                "task",
                false);
            checkResources(
                membership.getReferencedResources()
                    .getTasks(),
                tasks,
                null,
                "task",
                allowUnresolvedExternal);
        }
        validateGraphReferences(stories, sessions, tasks, memberships, resolveStoryTargets);
    }

    private static void checkTypedKinds(CanonicalStoryMembership membership, Set<String> actorIds, Set<String> itemIds,
        Set<String> itemGroupIds, Set<String> sessionIds, Set<String> taskIds) {
        checkWrongKind(
            membership.getOwnedResources()
                .getActors(),
            actorIds,
            itemIds,
            "actor");
        checkWrongKind(
            membership.getOwnedResources()
                .getActors(),
            actorIds,
            itemGroupIds,
            "actor");
        checkWrongKind(
            membership.getOwnedResources()
                .getActors(),
            actorIds,
            sessionIds,
            "actor");
        checkWrongKind(
            membership.getOwnedResources()
                .getActors(),
            actorIds,
            taskIds,
            "actor");
        checkWrongKind(
            membership.getReferencedResources()
                .getActors(),
            actorIds,
            itemIds,
            "actor");
        checkWrongKind(
            membership.getReferencedResources()
                .getActors(),
            actorIds,
            itemGroupIds,
            "actor");
        checkWrongKind(
            membership.getReferencedResources()
                .getActors(),
            actorIds,
            sessionIds,
            "actor");
        checkWrongKind(
            membership.getReferencedResources()
                .getActors(),
            actorIds,
            taskIds,
            "actor");
        checkWrongKind(
            membership.getOwnedResources()
                .getItems(),
            itemIds,
            actorIds,
            "item");
        checkWrongKind(
            membership.getOwnedResources()
                .getItems(),
            itemIds,
            itemGroupIds,
            "item");
        checkWrongKind(
            membership.getReferencedResources()
                .getItems(),
            itemIds,
            actorIds,
            "item");
        checkWrongKind(
            membership.getReferencedResources()
                .getItems(),
            itemIds,
            itemGroupIds,
            "item");
        checkWrongKind(
            membership.getOwnedResources()
                .getItemGroups(),
            itemGroupIds,
            actorIds,
            "item_group");
        checkWrongKind(
            membership.getOwnedResources()
                .getItemGroups(),
            itemGroupIds,
            itemIds,
            "item_group");
        checkWrongKind(
            membership.getReferencedResources()
                .getItemGroups(),
            itemGroupIds,
            actorIds,
            "item_group");
        checkWrongKind(
            membership.getReferencedResources()
                .getItemGroups(),
            itemGroupIds,
            itemIds,
            "item_group");
        checkWrongKind(
            membership.getOwnedResources()
                .getSessions(),
            sessionIds,
            actorIds,
            "session");
        checkWrongKind(
            membership.getOwnedResources()
                .getSessions(),
            sessionIds,
            itemIds,
            "session");
        checkWrongKind(
            membership.getOwnedResources()
                .getSessions(),
            sessionIds,
            itemGroupIds,
            "session");
        checkWrongKind(
            membership.getOwnedResources()
                .getSessions(),
            sessionIds,
            taskIds,
            "session");
        checkWrongKind(
            membership.getReferencedResources()
                .getSessions(),
            sessionIds,
            actorIds,
            "session");
        checkWrongKind(
            membership.getReferencedResources()
                .getSessions(),
            sessionIds,
            itemIds,
            "session");
        checkWrongKind(
            membership.getReferencedResources()
                .getSessions(),
            sessionIds,
            itemGroupIds,
            "session");
        checkWrongKind(
            membership.getReferencedResources()
                .getSessions(),
            sessionIds,
            taskIds,
            "session");
        checkWrongKind(
            membership.getOwnedResources()
                .getTasks(),
            taskIds,
            actorIds,
            "task");
        checkWrongKind(
            membership.getOwnedResources()
                .getTasks(),
            taskIds,
            itemIds,
            "task");
        checkWrongKind(
            membership.getOwnedResources()
                .getTasks(),
            taskIds,
            itemGroupIds,
            "task");
        checkWrongKind(
            membership.getOwnedResources()
                .getTasks(),
            taskIds,
            sessionIds,
            "task");
        checkWrongKind(
            membership.getReferencedResources()
                .getTasks(),
            taskIds,
            actorIds,
            "task");
        checkWrongKind(
            membership.getReferencedResources()
                .getTasks(),
            taskIds,
            itemIds,
            "task");
        checkWrongKind(
            membership.getReferencedResources()
                .getTasks(),
            taskIds,
            itemGroupIds,
            "task");
        checkWrongKind(
            membership.getReferencedResources()
                .getTasks(),
            taskIds,
            sessionIds,
            "task");
    }

    private static void checkWrongKind(List<String> ids, Set<String> expected, Set<String> wrong, String kind) {
        for (String id : ids)
            if (!expected.contains(id) && wrong.contains(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".kind.mismatch",
                "Canonical membership declares '" + id
                    + "' as "
                    + kind
                    + " but its loaded definition has another kind.");
    }

    private static void checkActors(CanonicalStoryMembership membership, Set<String> actorIds, Set<String> ownedActors,
        boolean allowUnresolvedExternal) {
        for (String id : membership.getOwnedResources()
            .getActors()) {
            requireActor(actorIds, id);
            if (!ownedActors.add(id)) throw CanonicalProjectContentException.failure(
                "project.content.actor.ownership.duplicate",
                "Actor is owned by more than one canonical Story: " + id);
        }
        for (String id : membership.getReferencedResources()
            .getActors()) {
            if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                "project.content.actor.id.invalid",
                "Canonical membership contains an invalid actor ID: " + id);
            if (!actorIds.contains(id) && (!allowUnresolvedExternal || !DgrResourceId.isFullId(id)))
                requireActor(actorIds, id);
        }
    }

    private static void requireActor(Set<String> actorIds, String id) {
        if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException
            .failure("project.content.actor.id.invalid", "Canonical membership contains an invalid actor ID: " + id);
        if (!actorIds.contains(id)) throw CanonicalProjectContentException
            .failure("project.content.actor.missing", "Canonical membership references an unknown Actor: " + id);
    }

    private static void checkIdentityResources(List<String> ids, Set<String> known, Set<String> owned, String kind,
        boolean allowUnresolvedExternal) {
        for (String id : ids) {
            if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".id.invalid",
                "Canonical membership contains an invalid " + kind + " ID: " + id);
            if (!known.contains(id) && (owned != null || !allowUnresolvedExternal || !DgrResourceId.isFullId(id)))
                throw CanonicalProjectContentException.failure(
                    "project.content." + kind + ".missing",
                    "Canonical membership references an unknown " + kind + ": " + id);
            if (owned != null && !owned.add(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".ownership.duplicate",
                kind + " is owned by more than one canonical Story: " + id);
        }
    }

    private static void checkResources(List<String> ids, Map<String, CanonicalGraphResource> resources,
        Set<String> owned, String kind, boolean allowUnresolvedExternal) {
        for (String id : ids) {
            if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".id.invalid",
                "Canonical membership contains an invalid " + kind + " ID: " + id);
            if (!resources.containsKey(id)
                && (owned != null || !allowUnresolvedExternal || !DgrResourceId.isFullId(id)))
                throw CanonicalProjectContentException.failure(
                    "project.content." + kind + ".missing",
                    "Canonical membership references an unknown " + kind + ": " + id);
            if (owned != null && !owned.add(id)) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".ownership.duplicate",
                kind + " is owned by more than one canonical Story: " + id);
        }
    }

    private static void checkKinds(Map<String, CanonicalGraphResource> resources, CanonicalGraphResourceKind expected,
        String kind) {
        for (Map.Entry<String, CanonicalGraphResource> entry : resources.entrySet()) {
            CanonicalGraphResource value = entry.getValue();
            if (value == null || !entry.getKey()
                .equals(value.getId()) || !DgrResourceId.isCompatibleId(entry.getKey()))
                throw CanonicalProjectContentException.failure(
                    "project.content." + kind + ".id.mismatch",
                    "Canonical " + kind + " map key does not match its resource ID: " + entry.getKey());
            if (value.getResourceKind() != expected) throw CanonicalProjectContentException.failure(
                "project.content." + kind + ".kind.mismatch",
                "Canonical resource '" + entry.getKey()
                    + "' has kind '"
                    + (value.getResourceKind() == null ? "null"
                        : value.getResourceKind()
                            .getJsonName())
                    + "', expected '"
                    + expected.getJsonName()
                    + "'.");
        }
    }

    private static void validateGraphReferences(Map<String, CanonicalGraphResource> stories,
        Map<String, CanonicalGraphResource> sessions, Map<String, CanonicalGraphResource> tasks,
        Map<String, CanonicalStoryMembership> memberships, boolean resolveStoryTargets) {
        for (CanonicalGraphResource story : stories.values()) {
            CanonicalStoryMembership membership = memberships.get(story.getId());
            if (membership != null) validateStoryGraph(story, declarations(membership), stories, resolveStoryTargets);
        }
        for (CanonicalStoryMembership membership : memberships.values()) {
            ResourceDeclarations declared = declarations(membership);
            for (String id : declared.sessions) {
                CanonicalGraphResource session = sessions.get(id);
                if (session != null) validateSessionGraph(session, declared);
            }
            for (String id : declared.tasks) {
                CanonicalGraphResource task = tasks.get(id);
                if (task != null) validateTaskGraph(task, declared);
            }
        }
    }

    private static ResourceDeclarations declarations(CanonicalStoryMembership membership) {
        ResourceDeclarations result = new ResourceDeclarations();
        add(
            result.actors,
            membership.getOwnedResources()
                .getActors());
        add(
            result.actors,
            membership.getReferencedResources()
                .getActors());
        add(
            result.items,
            membership.getOwnedResources()
                .getItems());
        add(
            result.items,
            membership.getReferencedResources()
                .getItems());
        add(
            result.itemGroups,
            membership.getOwnedResources()
                .getItemGroups());
        add(
            result.itemGroups,
            membership.getReferencedResources()
                .getItemGroups());
        add(
            result.sessions,
            membership.getOwnedResources()
                .getSessions());
        add(
            result.sessions,
            membership.getReferencedResources()
                .getSessions());
        add(
            result.tasks,
            membership.getOwnedResources()
                .getTasks());
        add(
            result.tasks,
            membership.getReferencedResources()
                .getTasks());
        return result;
    }

    private static void validateStoryGraph(CanonicalGraphResource story, ResourceDeclarations declared,
        Map<String, CanonicalGraphResource> stories, boolean resolveStoryTargets) {
        if (story.getGraph() == null) return;
        for (CanonicalGraphNode node : story.getGraph()
            .getNodes()) {
            if ("interact_actor".equals(node.getType()) || "enter_region".equals(node.getType())
                || "enter_story".equals(node.getType()))
                throw CanonicalProjectContentException.failure(
                    "project.content.story.standalone_node.removed",
                    "Retired standalone Story node is unsupported: " + node.getType());
            if ("session".equals(node.getType())) requireProperty(node, "resource_id", declared.sessions, "session");
            else if ("task".equals(node.getType())) requireProperty(node, "resource_id", declared.tasks, "task");
            else if ("action".equals(node.getType()) && text(node, "action_type").equals("give_item"))
                requireProperty(node, "item_id", declared.items, "item");
            else if ("start".equals(node.getType())) validateStartTriggers(node, declared);
        }
    }

    private static void validateStartTriggers(CanonicalGraphNode node, ResourceDeclarations declared) {
        JsonElement value = node.getProperties()
            .get("triggers");
        if (value == null || !value.isJsonArray()) return;
        for (JsonElement triggerValue : value.getAsJsonArray()) {
            if (!triggerValue.isJsonObject()) continue;
            JsonObject trigger = triggerValue.getAsJsonObject();
            JsonElement type = trigger.get("trigger_type");
            if (type == null || !type.isJsonPrimitive() || !"interact_actor".equals(type.getAsString())) continue;
            JsonElement properties = trigger.get("trigger_properties");
            if (properties != null && properties.isJsonObject()) {
                JsonElement actor = properties.getAsJsonObject()
                    .get("actor_id");
                if (actor != null) requireDeclared(actor, declared.actors, "actor", node.getId());
            }
        }
    }

    private static void validateSessionGraph(CanonicalGraphResource session, ResourceDeclarations declared) {
        if (session.getGraph() == null) return;
        for (CanonicalGraphNode node : session.getGraph()
            .getNodes()) if ("line".equals(node.getType())) {
                String speaker = darkgrey.rpg.session.runtime.CanonicalSessionRuntime
                    .optionalLineString(node, "speaker_actor_id");
                if (speaker != null) requireProperty(node, "speaker_actor_id", declared.actors, "actor");
            }
    }

    private static void validateTaskGraph(CanonicalGraphResource task, ResourceDeclarations declared) {
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes())
            if ("reward".equals(node.getType()))
                for (darkgrey.rpg.task.runtime.CanonicalTaskRewardPackage.Entry entry : darkgrey.rpg.task.runtime.CanonicalTaskRewardPackage
                    .read(node))
                    if ("item".equals(entry.getType()) && !declared.items.contains(entry.getItem()))
                        throw undeclared("item", entry.getItem(), node.getId());
        if (task.getGraph() == null) return;
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes()) if ("objective".equals(node.getType())) {
                String type = text(node, "objective_type");
                if ("interact_actor".equals(type)) requireProperty(node, "actor_id", declared.actors, "actor");
                else if ("collect_item".equals(type) || "submit_item".equals(type)) {
                    String id = property(node, "item");
                    if (id != null && !nativeTarget(id)) {
                        if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                            "project.content.graph.reference.invalid",
                            "Graph node '" + node.getId() + "' has an invalid item ID.");
                        if (!declared.items.contains(id) && !declared.itemGroups.contains(id))
                            throw undeclared("item", id, node.getId());
                    }
                    if ("submit_item".equals(type)) {
                        JsonElement actor = node.getProperties()
                            .get("actor_id");
                        if (actor == null) throw CanonicalProjectContentException.failure(
                            "project.content.graph.reference.missing",
                            "Graph node '" + node.getId() + "' is missing actor reference.");
                        requireDeclared(actor, declared.actors, "actor", node.getId());
                    }
                } else if ("kill_entity".equals(type)) {
                    String id = property(node, "entity");
                    if (id != null && !nativeTarget(id)) {
                        if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                            "project.content.graph.reference.invalid",
                            "Graph node '" + node.getId() + "' has an invalid actor ID.");
                        if (!declared.actors.contains(id)) throw undeclared("actor", id, node.getId());
                    }
                }
            }
    }

    private static void requireProperty(CanonicalGraphNode node, String field, Set<String> declared, String kind) {
        String id = property(node, field);
        if (id != null) {
            if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
                "project.content.graph.reference.invalid",
                "Graph node '" + node.getId() + "' has an invalid " + kind + " ID.");
            if (!declared.contains(id)) throw undeclared(kind, id, node.getId());
        }
    }

    private static void requireDeclared(JsonElement value, Set<String> declared, String kind, String nodeId) {
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString())
            throw CanonicalProjectContentException.failure(
                "project.content.graph.reference.invalid",
                "Graph node '" + nodeId + "' has a non-string " + kind + " reference.");
        String id = value.getAsString();
        if (id.trim()
            .isEmpty()) return;
        if (!DgrResourceId.isCompatibleId(id)) throw CanonicalProjectContentException.failure(
            "project.content.graph.reference.invalid",
            "Graph node '" + nodeId + "' has an invalid " + kind + " ID.");
        if (!declared.contains(id)) throw undeclared(kind, id, nodeId);
    }

    private static String property(CanonicalGraphNode node, String field) {
        JsonElement value = node.getProperties()
            .get(field);
        if (value == null) return null;
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive()
            .isString())
            throw CanonicalProjectContentException.failure(
                "project.content.graph.reference.invalid",
                "Graph node '" + node.getId() + "' has a non-string reference property '" + field + "'.");
        String id = value.getAsString();
        return id.trim()
            .isEmpty() ? null : id;
    }

    private static String text(CanonicalGraphNode node, String field) {
        JsonElement value = node.getProperties()
            .get(field);
        return value != null && value.isJsonPrimitive()
            && value.getAsJsonPrimitive()
                .isString() ? value.getAsString() : "";
    }

    private static boolean nativeTarget(String id) {
        return !id.contains(":") || id.startsWith("minecraft:");
    }

    private static CanonicalProjectContentException undeclared(String kind, String id, String nodeId) {
        return CanonicalProjectContentException.failure(
            "project.content.graph.reference.undeclared",
            "Graph node '" + nodeId + "' references undeclared " + kind + " '" + id + "'.");
    }

    private static CanonicalProjectContentException missing(String kind, String id, String message) {
        return CanonicalProjectContentException.failure("project.content." + kind + ".missing", message + ": " + id);
    }

    private static void add(Set<String> target, List<String> values) {
        target.addAll(values);
    }

    private static void merge(ResourceDeclarations target, ResourceDeclarations source) {
        target.actors.addAll(source.actors);
        target.items.addAll(source.items);
        target.itemGroups.addAll(source.itemGroups);
        target.sessions.addAll(source.sessions);
        target.tasks.addAll(source.tasks);
    }

    private static final class ResourceDeclarations {

        private final Set<String> actors = new HashSet<String>();
        private final Set<String> items = new HashSet<String>();
        private final Set<String> itemGroups = new HashSet<String>();
        private final Set<String> sessions = new HashSet<String>();
        private final Set<String> tasks = new HashSet<String>();
    }
}
