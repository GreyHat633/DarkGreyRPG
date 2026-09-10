package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;

/** Pure B4 compatibility probe for project origins and namespace ownership warnings. */
public final class OfflineOriginProbe {

    private OfflineOriginProbe() {}

    public static void main(String[] args) throws Exception {
        assertProjectOriginParsing();
        assertNamespaceOriginWarnings();
        String fixture = System.getenv("DGR_OFFLINE_LIVE_FIXTURE");
        if (fixture != null && !fixture.isEmpty()) {
            java.nio.file.Path root = java.nio.file.Paths.get(fixture);
            java.nio.file.Path install = root.resolve("runtime-install");
            java.nio.file.Files.createDirectories(install);
            java.nio.file.Files.copy(
                root.resolve("B-v1.dgrs"),
                install.resolve("B.dgrs"),
                java.nio.file.StandardCopyOption.REPLACE_EXISTING);
            StoryPackageLoader loader = new StoryPackageLoader(install.toFile());
            StoryPackageLoader.ReloadResult loaded = loader.load();
            require(loaded.isSuccessful(), "Studio package failed runtime load: " + loaded.getErrors());
            String expected = new String(
                java.nio.file.Files.readAllBytes(root.resolve("fingerprint.txt")),
                StandardCharsets.UTF_8).trim();
            require(
                expected.equals(
                    loader.getPackage("B:story")
                        .getContentFingerprint()),
                "Studio and runtime package fingerprints differ");
            System.out.println("STUDIO_EXPORTED_ARCHIVE_RUNTIME_LOAD=PASS");
            System.out.println("STUDIO_RUNTIME_CONTENT_FINGERPRINT=PASS");
        }
        System.out.println("PROJECT_ORIGIN_EXPLICIT_PARSE=PASS");
        System.out.println("PROJECT_ORIGIN_LEGACY_ID_FALLBACK=PASS");
        System.out.println("PROJECT_ORIGIN_STRICT_OPTIONAL_FIELD=PASS");
        System.out.println("PROJECT_ORIGIN_OWNED_NAMESPACE_WARNING=PASS");
        System.out.println("PROJECT_ORIGIN_REFERENCED_NAMESPACE_EXCLUDED=PASS");
    }

    private static void assertProjectOriginParsing() throws Exception {
        for (String id : new String[] { "TestProject2", "testproject2" }) {
            ProjectDefinition project = ProjectRepository.readPackagedProject(
                json("{\"schema_version\":1,\"id\":\"" + id + "\",\"display_name\":\"Probe\"}"),
                "case-project.json");
            require(id.equals(project.getId()), "Project ID casing was changed");
        }
        System.out.println("PROJECT_ID_CASE_PRESERVED=PASS");
        ProjectDefinition explicit = ProjectRepository.readPackagedProject(
            json(
                "{\"schema_version\":2,\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                    + "\"project_origin_code\":\" origin-a \"}"),
            "explicit-project.json");
        require(" origin-a ".equals(explicit.getProjectOriginCode()), "Explicit project origin was not preserved");

        ProjectDefinition legacy = ProjectRepository.readPackagedProject(
            json("{\"schema_version\":1,\"id\":\"legacy-id\",\"display_name\":\"Probe\"}"),
            "legacy-project.json");
        require("legacy-id".equals(legacy.getProjectOriginCode()), "Legacy project origin did not fall back to id");

        expectProjectLoad(
            "{\"schema_version\":1,\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                + "\"project_origin_code\":\"   \"}",
            "Blank project_origin_code was accepted");
        expectProjectLoad(
            "{\"schema_version\":1,\"id\":\"legacy-id\",\"display_name\":\"Probe\"," + "\"project_origin_code\":17}",
            "Non-string project_origin_code was accepted");
        expectProjectLoad(
            "{\"schema_version\":1,\"id\":\"legacy-id\",\"display_name\":\"Probe\"," + "\"project_origin_code\":null}",
            "Null project_origin_code was accepted");
    }

    private static void assertNamespaceOriginWarnings() throws Exception {
        LoadedStoryPackage first = packageValue("package-a", "a:story", "origin-a", "z:external");
        LoadedStoryPackage second = packageValue("package-b", "a:other", "origin-b", "z:external");
        List<String> warnings = StoryPackageSnapshotMerger.findNamespaceOriginWarnings(mapOf(first, second));
        require(warnings.size() == 1, "Expected one namespace origin warning: " + warnings);
        require(
            warnings.get(0)
                .contains("Namespace 'a'"),
            "Warning named the wrong namespace: " + warnings);
        require(
            warnings.get(0)
                .contains("origin-a")
                && warnings.get(0)
                    .contains("origin-b"),
            "Warning omitted one of the project origins: " + warnings);
        require(
            !warnings.get(0)
                .contains("z"),
            "Referenced-only namespace was claimed: " + warnings);
        require(
            StoryPackageSnapshotMerger.namespaceOriginWarnings(mapOf(first, second))
                .equals(warnings),
            "Warning alias changed the pure result");
    }

    private static LoadedStoryPackage packageValue(String packageId, String storyId, String origin,
        String referencedActor) throws Exception {
        String storyPath = "resources/canonical/stories/story.json";
        String membershipPath = "resources/canonical/memberships/story.json";
        StoryPackageManifest manifest = StoryPackageManifest.read(
            ("{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\","
                + "\"producer_version\":\"probe\",\"schema_version\":1,\"package_id\":\""
                + packageId
                + "\",\"package_version\":\"1\",\"story_id\":\""
                + storyId
                + "\",\"story_schema_version\":1,\"required_resources\":{\"story\":\""
                + storyPath
                + "\",\"canonical_stories\":[\""
                + storyPath
                + "\"],\"canonical_memberships\":[\""
                + membershipPath
                + "\"]}}").getBytes(StandardCharsets.UTF_8),
            "manifest.json");
        CanonicalGraphResource story = new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            storyId,
            "Story",
            null);
        CanonicalStoryMembership membership = new CanonicalStoryMembership(
            storyId,
            new CanonicalStoryMembershipSet(
                Collections.singletonList(storyId.substring(0, storyId.indexOf(':')) + ":owned"),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList()),
            new CanonicalStoryMembershipSet(
                Collections.singletonList(referencedActor),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList(),
                Collections.<String>emptyList()));
        Map<String, CanonicalGraphResource> stories = singleton(storyId, story);
        Map<String, CanonicalStoryMembership> memberships = singleton(storyId, membership);
        CanonicalProjectContent canonical = new CanonicalProjectContent(
            stories,
            Collections.<String, CanonicalGraphResource>emptyMap(),
            Collections.<String, CanonicalGraphResource>emptyMap(),
            memberships,
            CanonicalStoryLogicGraph.empty());
        ProjectSnapshot snapshot = new ProjectSnapshot(
            new ProjectDefinition(2, packageId, packageId, origin),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            canonical);
        Map<String, byte[]> bytes = new LinkedHashMap<String, byte[]>();
        bytes.put(
            "project.json",
            json(
                "{\"schema_version\":2,\"id\":\"" + packageId
                    + "\",\"display_name\":\"Probe\","
                    + "\"project_origin_code\":\""
                    + origin
                    + "\"}"));
        bytes.put(storyPath, json("{\"id\":\"" + storyId + "\"}"));
        bytes.put(membershipPath, json("{\"story_id\":\"" + storyId + "\"}"));
        return new LoadedStoryPackage(manifest, null, snapshot, CanonicalStoryLogicGraph.empty(), bytes);
    }

    private static byte[] json(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static Map<String, LoadedStoryPackage> mapOf(LoadedStoryPackage first, LoadedStoryPackage second) {
        Map<String, LoadedStoryPackage> result = new LinkedHashMap<String, LoadedStoryPackage>();
        result.put(first.getPackageId(), first);
        result.put(second.getPackageId(), second);
        return result;
    }

    private static <T> Map<String, T> singleton(String key, T value) {
        Map<String, T> result = new LinkedHashMap<String, T>();
        result.put(key, value);
        return result;
    }

    private static void expectProjectLoad(String source, String message) throws Exception {
        try {
            ProjectRepository.readPackagedProject(json(source), "invalid-project.json");
            throw new AssertionError(message);
        } catch (ProjectLoadException expected) {}
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
