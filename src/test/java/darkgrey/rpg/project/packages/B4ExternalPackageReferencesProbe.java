package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentException;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectSnapshot;

/** Focused Phase 9 proof for deferred external DGRS references and final merge resolution. */
public final class B4ExternalPackageReferencesProbe {

    private B4ExternalPackageReferencesProbe() {}

    public static void main(String[] args) throws Exception {
        CanonicalProjectContentLoaderProbe();
        System.out.println("DGRS_EXTERNAL_REFERENCE_PACKAGE_LOAD=PASS");
        System.out.println("DGRS_EXTERNAL_REFERENCE_MERGED_RESOLVE=PASS");
        System.out.println("DGRS_EXTERNAL_REFERENCE_MISSING_REJECTS_MERGE=PASS");
        System.out.println("DGRS_EXTERNAL_REFERENCE_OWNED_REQUIRED=PASS");
        System.out.println("DGRS_EXTERNAL_REFERENCE_TYPED_DECLARATION=PASS");
        System.out.println("DGRS_EXTERNAL_REFERENCE_KIND_VALIDATION=PASS");
    }

    private static void CanonicalProjectContentLoaderProbe() throws Exception {
        String providerActor = "provider:guard";
        String consumerStory = "consumer:story";
        CanonicalGraphResource consumer = story(consumerStory, providerActor);
        CanonicalStoryMembership consumerMembership = membership(
            consumerStory,
            new CanonicalStoryMembershipSet(
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList()),
            new CanonicalStoryMembershipSet(
                Collections.singletonList(providerActor),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList()));
        Map<String, CanonicalGraphResource> stories = singleton(consumerStory, consumer);
        Map<String, CanonicalStoryMembership> memberships = singleton(consumerStory, consumerMembership);
        CanonicalProjectContent partial = new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader()
            .loadPackageContentPartial(
                stories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships,
                Collections.<String>emptySet(),
                Collections.<String>emptySet(),
                Collections.<String>emptySet());
        require(partial.getStory(providerActor) == null, "Partial package unexpectedly materialized external Actor");

        LoadedStoryPackage consumerPackage = packageValue(
            "consumer:package",
            consumerStory,
            consumer,
            consumerMembership,
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.<String, byte[]>emptyMap());
        expectProjectLoad(new RunnableWithProjectLoad() {

            @Override
            public void run() throws ProjectLoadException {
                StoryPackageSnapshotMerger.merge(singleton(consumerPackage.getPackageId(), consumerPackage));
            }
        }, "missing external Actor was accepted by final merge");

        ActorDefinition actor = new ActorDefinition(
            2,
            ActorDefinition.TYPE_INDIVIDUAL,
            providerActor,
            "Guard",
            "",
            Collections.<String>emptyList(),
            "provider:story");
        CanonicalGraphResource provider = story("provider:story", null);
        CanonicalStoryMembership providerMembership = membership(
            "provider:story",
            new CanonicalStoryMembershipSet(
                Collections.singletonList(providerActor),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList()),
            new CanonicalStoryMembershipSet());
        LoadedStoryPackage providerPackage = packageValue(
            "provider:package",
            "provider:story",
            provider,
            providerMembership,
            singleton(providerActor, actor),
            singletonBytes("resources/actors/guard.json", actorJson(providerActor)));
        ProjectSnapshot merged = StoryPackageSnapshotMerger.merge(mapOf(providerPackage, consumerPackage));
        require(merged.getActor(providerActor) == actor, "Resolved external Actor did not enter merged snapshot");

        expectCanonicalFailure(new RunnableWithCanonicalFailure() {

            @Override
            public void run() {
                new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader().loadPackageContentPartial(
                    stories,
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    singleton(
                        consumerStory,
                        membership(
                            consumerStory,
                            new CanonicalStoryMembershipSet(
                                Collections.singletonList(providerActor),
                                Collections.<String>emptyList(),
                                Collections.<String>emptyList(),
                                Collections.<String>emptyList(),
                                Collections.<String>emptyList()),
                            new CanonicalStoryMembershipSet())),
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet());
            }
        }, "project.content.actor.missing", "missing owned Actor was accepted");

        expectCanonicalFailure(new RunnableWithCanonicalFailure() {

            @Override
            public void run() {
                new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader().loadPackageContentPartial(
                    singleton(consumerStory, story(consumerStory, "consumer:undeclared")),
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    memberships,
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet());
            }
        }, "project.content.graph.reference.undeclared", "undeclared typed graph Actor was accepted");

        Map<String, CanonicalGraphResource> wrongKind = singleton(
            "consumer:wrong",
            new CanonicalGraphResource(1, CanonicalGraphResourceKind.STORY, "consumer:wrong", "Wrong", null));
        expectCanonicalFailure(new RunnableWithCanonicalFailure() {

            @Override
            public void run() {
                new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader().loadPackageContentPartial(
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    wrongKind,
                    Collections.<String, CanonicalGraphResource>emptyMap(),
                    Collections.<String, CanonicalStoryMembership>emptyMap(),
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet(),
                    Collections.<String>emptySet());
            }
        }, "project.content.session.kind.mismatch", "canonical Session kind mismatch was accepted");
    }

    private static CanonicalGraphResource story(String id, String actorId) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        if (actorId != null) properties.put("actor_id", json(actorId));
        CanonicalGraphNode start = new CanonicalGraphNode(
            "start",
            "start",
            "Start",
            Collections.emptyList(),
            Collections.<String, JsonElement>emptyMap());
        CanonicalGraphNode actor = actorId == null ? null
            : new CanonicalGraphNode("actor", "interact_actor", "Actor", Collections.emptyList(), properties);
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            id,
            "Story",
            new CanonicalGraph(
                actor == null ? Collections.singletonList(start) : Arrays.asList(start, actor),
                Collections.emptyList()));
    }

    private static CanonicalStoryMembership membership(String storyId, CanonicalStoryMembershipSet owned,
        CanonicalStoryMembershipSet referenced) {
        return new CanonicalStoryMembership(storyId, owned, referenced);
    }

    private static LoadedStoryPackage packageValue(String packageId, String storyId, CanonicalGraphResource story,
        CanonicalStoryMembership membership, Map<String, ActorDefinition> actors, Map<String, byte[]> extraBytes)
        throws Exception {
        String storyPath = "resources/canonical/stories/story.json";
        String membershipPath = "resources/canonical/memberships/story.json";
        Map<String, byte[]> bytes = new LinkedHashMap<String, byte[]>();
        bytes.put(
            "project.json",
            "{\"schema_version\":2,\"id\":\"probe\",\"display_name\":\"Probe\"}".getBytes(StandardCharsets.UTF_8));
        bytes.put(storyPath, canonicalJson(story.getId()).getBytes(StandardCharsets.UTF_8));
        bytes.put(membershipPath, membershipJson(membership.getStoryId()).getBytes(StandardCharsets.UTF_8));
        bytes.putAll(extraBytes);
        String actorPaths = actors.isEmpty() ? "" : "\"actors\":[\"resources/actors/guard.json\"],";
        String manifestText = "{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\","
            + "\"producer_version\":\"probe\",\"schema_version\":1,\"package_id\":\""
            + packageId
            + "\",\"package_version\":\"1\",\"story_id\":\""
            + storyId
            + "\",\"story_schema_version\":1,\"required_resources\":{"
            + "\"story\":\""
            + storyPath
            + "\","
            + actorPaths
            + "\"canonical_stories\":[\""
            + storyPath
            + "\"],\"canonical_memberships\":[\""
            + membershipPath
            + "\"],\"sessions\":[],\"tasks\":[]}}";
        StoryPackageManifest manifest = StoryPackageManifest
            .read(manifestText.getBytes(StandardCharsets.UTF_8), "manifest.json");
        CanonicalProjectContent canonical = new darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader()
            .loadPackageContentPartial(
                singleton(story.getId(), story),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                singleton(membership.getStoryId(), membership),
                actors.keySet(),
                Collections.<String>emptySet(),
                Collections.<String>emptySet());
        ProjectSnapshot snapshot = new ProjectSnapshot(
            new ProjectDefinition(2, packageId, packageId),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            canonical);
        return new LoadedStoryPackage(
            manifest,
            null,
            snapshot,
            darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph.empty(),
            bytes);
    }

    private static String canonicalJson(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"story\",\"id\":\"" + id
            + "\",\"display_name\":\"Story\",\"graph\":{\"nodes\":[],\"connections\":[]}}";
    }

    private static String membershipJson(String storyId) {
        return "{\"schema_version\":3,\"story_id\":\"" + storyId
            + "\",\"owned_resources\":{\"actors\":[],\"items\":[],\"item_groups\":[],\"sessions\":[],\"tasks\":[]},"
            + "\"referenced_resources\":{\"actors\":[],\"items\":[],\"item_groups\":[],\"sessions\":[],\"tasks\":[]}}";
    }

    private static String actorJson(String id) {
        return "{\"schema_version\":2,\"type\":\"individual\",\"npc_id\":\"" + id
            + "\",\"display_name\":\"Guard\",\"tags\":[],\"home_story_id\":\"provider:story\"}";
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static <T> Map<String, T> singleton(String key, T value) {
        Map<String, T> result = new LinkedHashMap<String, T>();
        result.put(key, value);
        return result;
    }

    private static Map<String, byte[]> singletonBytes(String key, String value) {
        return singleton(key, value.getBytes(StandardCharsets.UTF_8));
    }

    private static Map<String, LoadedStoryPackage> mapOf(LoadedStoryPackage first, LoadedStoryPackage second) {
        Map<String, LoadedStoryPackage> result = new LinkedHashMap<String, LoadedStoryPackage>();
        result.put(first.getPackageId(), first);
        result.put(second.getPackageId(), second);
        return result;
    }

    private static void expectProjectLoad(RunnableWithProjectLoad action, String message) throws Exception {
        try {
            action.run();
            throw new AssertionError(message);
        } catch (ProjectLoadException expected) {}
    }

    private static void expectCanonicalFailure(RunnableWithCanonicalFailure action, String code, String message) {
        try {
            action.run();
            throw new AssertionError(message);
        } catch (CanonicalProjectContentException expected) {
            require(code.equals(expected.getCode()), "Expected " + code + ", got " + expected.getCode());
        }
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private interface RunnableWithProjectLoad {

        void run() throws ProjectLoadException;
    }

    private interface RunnableWithCanonicalFailure {

        void run();
    }
}
