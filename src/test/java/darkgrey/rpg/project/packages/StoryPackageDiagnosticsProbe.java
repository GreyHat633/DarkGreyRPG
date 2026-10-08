package darkgrey.rpg.project.packages;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectSnapshot;

/** Exercises real merge rejection, resource/field context, lossless pagination and installed DGRS copies. */
public final class StoryPackageDiagnosticsProbe {

    public static void main(String[] args) throws Exception {
        LoadedStoryPackage first = fixture("alpha", "ST-2345-6789-ABCD-EFGH", "剧情甲", "甲归属", 3);
        LoadedStoryPackage second = fixture("beta", "ST-JKLM-NPQR-STUV-WXYZ", "剧情乙", "乙归属", 5);
        Map<String, LoadedStoryPackage> pair = pair(first, second);
        String report = String.join("\n", StoryPackageConflictDiagnostics.describe(pair));
        require(report.contains("共 3 项"), "all shared conflicts collected, not just first Actor");
        for (String value : Arrays.asList(
            "剧情甲",
            "剧情乙",
            "alpha.dgrs",
            "beta.dgrs",
            "ST-2345-6789-ABCD-EFGH~actor~slimes",
            "ST-2345-6789-ABCD-EFGH~actor~boss",
            "ST-2345-6789-ABCD-EFGH~task~task",
            "display_name",
            "甲归属",
            "乙归属",
            "收集宝藏",
            "使用位置 A",
            "使用位置 B",
            "目标节点",
            "graph.nodes",
            "required_count",
            "A=3",
            "B=5")) require(report.contains(value), "missing detail: " + value);
        try {
            StoryPackageSnapshotMerger.merge(pair);
            throw new AssertionError("conflicting definitions must remain rejected");
        } catch (ProjectLoadException expected) {
            require(
                expected.getMessage()
                    .contains("共 3 项"),
                "merge carries detailed report");
        }
        LoadedStoryPackage identical = fixture("gamma", "ST-AAAA-BBBB-CCCC-DDDD", "剧情丙", "甲归属", 3);
        require(
            StoryPackageConflictDiagnostics.describe(pair(first, identical))
                .isEmpty(),
            "byte-identical sharing remains accepted");
        LoadedStoryPackage duplicateStory = fixture("duplicate", "ST-2345-6789-ABCD-EFGH", "剧情甲", "甲归属", 3);
        require(
            String.join("\n", StoryPackageConflictDiagnostics.describe(pair(first, duplicateStory)))
                .contains("重复定义"),
            "exclusive Story identity still rejects identical definitions");
        testFormattingOnly();
        testPages(report);
        if (args.length > 0) testInstalledCopies(new File(args[0]));
        System.out.println("PACKAGE_CONFLICT_ALL_RESOURCES_STORIES_TASKS_FIELDS=PASS");
        System.out.println("PACKAGE_CONFLICT_VALIDATION_RULES_UNCHANGED=PASS");
        System.out.println("PACKAGE_ERROR_CHAT_PAGINATION_SANITIZATION=PASS");
    }

    private static LoadedStoryPackage fixture(String packageId, String storyId, String storyName, String home,
        int count) throws Exception {
        Map<String, byte[]> bytes = new LinkedHashMap<String, byte[]>();
        bytes.put("project.json", utf8("{\"schema_version\":1,\"id\":\"probe\",\"display_name\":\"Probe\"}"));
        bytes.put(
            "stories/story.json",
            utf8(
                CurrentPackageProbeFixtures.story(storyId, storyName)
                    .toString()));
        bytes.put("actors/slimes.json", utf8(actor("ST-2345-6789-ABCD-EFGH~actor~slimes", "史莱姆群", home)));
        bytes.put("actors/boss.json", utf8(actor("ST-2345-6789-ABCD-EFGH~actor~boss", "酒馆老板", home)));
        String taskJson = "{\"id\":\"ST-2345-6789-ABCD-EFGH~task~task\",\"display_name\":\"收集宝藏\",\"graph\":{\"nodes\":["
            + "{\"id\":\"objective\",\"display_name\":\"目标节点\",\"properties\":{\"actor_id\":\"ST-2345-6789-ABCD-EFGH~actor~slimes\","
            + "\"nested\":{\"actor_id\":\"ST-2345-6789-ABCD-EFGH~actor~boss\"},\"required_count\":"
            + count
            + "}}]}}";
        com.google.gson.JsonObject taskWire = new JsonParser().parse(taskJson)
            .getAsJsonObject();
        taskWire.add("id", CurrentPackageProbeFixtures.address("ST-2345-6789-ABCD-EFGH~task~task"));
        taskWire.getAsJsonObject("graph")
            .add("connections", new com.google.gson.JsonArray());
        taskWire.getAsJsonObject("graph")
            .getAsJsonArray("nodes")
            .get(0)
            .getAsJsonObject()
            .add("ports", new com.google.gson.JsonArray());
        taskWire.getAsJsonObject("graph")
            .getAsJsonArray("nodes")
            .get(0)
            .getAsJsonObject()
            .addProperty("type", "objective");
        bytes.put("resources/canonical/tasks/task.json", utf8(taskWire.toString()));
        com.google.gson.JsonObject manifestJson = CurrentPackageProbeFixtures.manifest(storyId);
        com.google.gson.JsonObject resources = manifestJson.getAsJsonObject("required_resources");
        String storyPath = CurrentPackageProbeFixtures.storyPath(storyId);
        bytes.put(storyPath, bytes.remove("stories/story.json"));
        bytes.put(
            CurrentPackageProbeFixtures.membershipPath(storyId),
            utf8(
                CurrentPackageProbeFixtures.membership(storyId)
                    .toString()));
        resources.add("actors", CurrentPackageProbeFixtures.list("actors/slimes.json", "actors/boss.json"));
        resources.add("tasks", CurrentPackageProbeFixtures.list("resources/canonical/tasks/task.json"));
        StoryPackageManifest manifest = StoryPackageManifest.read(utf8(manifestJson.toString()), "manifest.json");
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("actor_id", new JsonParser().parse("\"ST-2345-6789-ABCD-EFGH~actor~slimes\""));
        properties.put("nested", new JsonParser().parse("{\"actor_id\":\"ST-2345-6789-ABCD-EFGH~actor~boss\"}"));
        CanonicalGraphResource task = new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.TASK,
            "ST-2345-6789-ABCD-EFGH~task~task",
            "收集宝藏",
            new CanonicalGraph(
                Collections.singletonList(
                    new CanonicalGraphNode("objective", "objective", "目标节点", Collections.emptyList(), properties)),
                Collections.emptyList()));
        CanonicalProjectContent content = new CanonicalProjectContent(
            Collections.singletonMap(
                storyId,
                new CanonicalGraphResource(
                    CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
                    CanonicalGraphResourceKind.STORY,
                    storyId,
                    storyName,
                    new CanonicalGraph(Collections.emptyList(), Collections.emptyList()))),
            Collections.emptyMap(),
            Collections.singletonMap(task.getId(), task),
            Collections.emptyMap(),
            darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph.empty());
        // Story names come from the payload/snapshot rather than inferred filenames.
        ProjectSnapshot snapshot = new ProjectSnapshot(
            new ProjectDefinition(1, packageId, packageId),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            content);
        return new LoadedStoryPackage(
            manifest,
            null,
            new File(packageId + ".dgrs"),
            snapshot,
            darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph.empty(),
            bytes);
    }

    private static void testFormattingOnly() throws Exception {
        LoadedStoryPackage a = fixture("first", "ST-2222-3333-4444-5555", "第一故事", "same", 3);
        LoadedStoryPackage b = fixture("second", "ST-3456-789A-BCDE-FGHJ", "第二故事", "same", 3);
        // Rebuild the candidate with the same fields but a different serialized representation.
        Map<String, byte[]> changed = new LinkedHashMap<String, byte[]>();
        for (String path : Arrays.asList(
            "project.json",
            b.getManifest()
                .getRequiredResources()
                .getStory(),
            b.getManifest()
                .getRequiredResources()
                .getCanonicalMemberships()
                .get(0),
            "actors/slimes.json",
            "actors/boss.json",
            "resources/canonical/tasks/task.json")) changed.put(path, b.getDeclaredResourceBytes(path));
        changed.put("actors/slimes.json", utf8("  " + actor("ST-2345-6789-ABCD-EFGH~actor~slimes", "史莱姆群", "same")));
        b = new LoadedStoryPackage(
            b.getManifest(),
            null,
            new File("second.dgrs"),
            b.getSnapshot(),
            b.getStoryLogicGraph(),
            changed);
        require(
            String.join("\n", StoryPackageConflictDiagnostics.describe(pair(a, b)))
                .contains("字段值相同"),
            "format-only conflicts explained without relaxing byte equality");
        changed.put(
            "actors/slimes.json",
            utf8(
                actor("ST-2345-6789-ABCD-EFGH~actor~slimes", "史莱姆群", "same")
                    .replace("\"tags\":[]", "\"tags\":[],\"default_portrait_ref\":null")));
        b = new LoadedStoryPackage(
            b.getManifest(),
            null,
            new File("second.dgrs"),
            b.getSnapshot(),
            b.getStoryLogicGraph(),
            changed);
        String missing = String.join("\n", StoryPackageConflictDiagnostics.describe(pair(a, b)));
        require(missing.contains("A=<字段不存在>") && missing.contains("B=null"), "missing differs from explicit null");
    }

    private static void testPages(String report) {
        List<String> reconstructed = new ArrayList<String>();
        StoryPackageErrorPage first = new StoryPackageErrorPage(Collections.singletonList(report), 1);
        for (int page = 1; page <= first.getPageCount(); page++) {
            StoryPackageErrorPage value = new StoryPackageErrorPage(Collections.singletonList(report), page);
            require(
                value.getLines()
                    .size() <= 8,
                "bounded chat page");
            reconstructed.addAll(value.getLines());
        }
        require(
            String.join("\n", reconstructed)
                .equals(report),
            "short report lines survive all pages without loss");
        StoryPackageErrorPage safe = new StoryPackageErrorPage(
            Collections.singletonList("\u00a7kname\twith\u0000control"),
            1);
        require(
            !safe.getLines()
                .get(0)
                .contains("\u00a7")
                && !safe.getLines()
                    .get(0)
                    .contains("\u0000"),
            "untrusted text cannot inject formatting");
        String longLine = String.join("", Collections.nCopies(159, "x")) + "\ud83d\udcd6"
            + String.join("", Collections.nCopies(350, "中"));
        StringBuilder restored = new StringBuilder();
        for (String line : new StoryPackageErrorPage(Collections.singletonList(longLine), 1).getLines()) {
            require(!Character.isHighSurrogate(line.charAt(line.length() - 1)), "surrogate pair is not cut");
            restored.append(line.startsWith("  ") ? line.substring(2) : line);
        }
        require(longLine.equals(restored.toString()), "long Unicode lines preserve complete contents");
        try {
            new StoryPackageErrorPage(Collections.singletonList(report), first.getPageCount() + 1);
            throw new AssertionError("invalid page accepted");
        } catch (IllegalArgumentException expected) {
            require(
                expected.getMessage()
                    .contains("页码"),
                "friendly page error");
        }
    }

    private static void testInstalledCopies(File directory) throws Exception {
        Path scratch = Files.createTempDirectory("dgr-package-diagnostics-");
        Map<String, LoadedStoryPackage> values = new LinkedHashMap<String, LoadedStoryPackage>();
        try {
            File[] archives = directory.listFiles((dir, name) -> name.endsWith(".dgrs"));
            require(archives != null && archives.length >= 2, "two real conflicting packages required");
            Arrays.sort(archives);
            for (int i = 0; i < archives.length; i++) {
                Path install = Files.createDirectories(scratch.resolve("Install" + i));
                Files.copy(archives[i].toPath(), install.resolve(archives[i].getName()));
                StoryPackageLoader loader = new StoryPackageLoader(
                    install.toFile(),
                    scratch.resolve("Cache")
                        .toFile());
                require(
                    loader.reload()
                        .isSuccessful(),
                    "individual real package is valid: " + loader.getLastReload()
                        .getErrors());
                values.putAll(loader.getPackages());
            }
            String report = String.join("\n", StoryPackageConflictDiagnostics.describe(values));
            System.out.println("REAL_PACKAGE_REPORT_BEGIN\n" + report + "\nREAL_PACKAGE_REPORT_END");
            require(
                report.contains("GreyHat_:Slimes") && report.contains("GreyHat_:TarvenBoss")
                    && report.contains("default_portrait_ref")
                    && report.contains("home_story_id"),
                "real package fields and both conflicts");
            Path combined = Files.createDirectories(scratch.resolve("Combined"));
            for (File archive : archives) Files.copy(archive.toPath(), combined.resolve(archive.getName()));
            StoryPackageLoader rejected = new StoryPackageLoader(
                combined.toFile(),
                scratch.resolve("Cache")
                    .toFile());
            require(
                !rejected.reload()
                    .isSuccessful() && rejected.getPackages()
                        .isEmpty(),
                "actual conflicting set remains rejected");
            testCommandPages(
                rejected,
                scratch.resolve("Base")
                    .toFile());
            System.out.println("REAL_INSTALLED_DGRS_INDIVIDUAL_LOAD_AND_CONFLICT_DETAILS=PASS");
        } finally {
            for (LoadedStoryPackage value : values.values()) value.close();
            try (java.util.stream.Stream<Path> paths = Files.walk(scratch)) {
                for (Path path : (Iterable<Path>) paths.sorted(java.util.Comparator.reverseOrder())::iterator)
                    Files.delete(path);
            }
        }
    }

    private static void testCommandPages(StoryPackageLoader loader, File base) {
        darkgrey.rpg.project.ProjectRepository repository = new darkgrey.rpg.project.ProjectRepository(base);
        darkgrey.rpg.command.CommandDarkGreyRpg command = new darkgrey.rpg.command.CommandDarkGreyRpg(
            repository,
            null,
            new darkgrey.rpg.session.forge.CanonicalSessionForgeManager(repository),
            null,
            null,
            loader);
        List<net.minecraft.util.IChatComponent> received = new ArrayList<net.minecraft.util.IChatComponent>();
        net.minecraft.command.ICommandSender sender = (net.minecraft.command.ICommandSender) java.lang.reflect.Proxy
            .newProxyInstance(
                StoryPackageDiagnosticsProbe.class.getClassLoader(),
                new Class<?>[] { net.minecraft.command.ICommandSender.class },
                (proxy, method, arguments) -> {
                    if ("addChatMessage".equals(method.getName()))
                        received.add((net.minecraft.util.IChatComponent) arguments[0]);
                    if (method.getReturnType() == boolean.class) return true;
                    if (method.getReturnType() == String.class) return "DiagnosticsProbe";
                    return null;
                });
        int pages = new StoryPackageErrorPage(
            loader.getLastReload()
                .getErrors(),
            1).getPageCount();
        for (int page = 1; page <= pages; page++) {
            command.processCommand(sender, new String[] { "reload", "errors", Integer.toString(page) });
        }
        String text = received.stream()
            .map(net.minecraft.util.IChatComponent::getUnformattedText)
            .collect(java.util.stream.Collectors.joining("\n"));
        require(
            text.contains("GreyHat_:Slimes") && text.contains("GreyHat_:TarvenBoss") && text.contains("默认头像"),
            "command actually sends report fields to chat");
        boolean clickable = false;
        for (net.minecraft.util.IChatComponent message : received) {
            for (Object sibling : message.getSiblings()) {
                net.minecraft.event.ClickEvent click = ((net.minecraft.util.IChatComponent) sibling).getChatStyle()
                    .getChatClickEvent();
                if (click != null && "/dgr reload errors 2".equals(click.getValue())) clickable = true;
            }
        }
        require(clickable, "next page has working command click event");
        System.out.println("PACKAGE_ERROR_COMMAND_ACTUAL_CHAT_COMPONENTS_AND_CLICK_EVENTS=PASS");
    }

    private static byte[] utf8(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static String actor(String id, String name, String home) {
        return CurrentPackageProbeFixtures.actor(id, name, home)
            .toString();
    }

    private static Map<String, LoadedStoryPackage> pair(LoadedStoryPackage a, LoadedStoryPackage b) {
        Map<String, LoadedStoryPackage> values = new LinkedHashMap<String, LoadedStoryPackage>();
        values.put("first", a);
        values.put("second", b);
        return values;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
