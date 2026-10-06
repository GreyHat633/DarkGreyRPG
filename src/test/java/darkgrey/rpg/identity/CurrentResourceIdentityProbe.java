package darkgrey.rpg.identity;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Collections;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;
import java.util.stream.Stream;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.nbt.NBTTagString;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalProjectContentLoader;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorSavedData;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.session.runtime.DynamicContentText;

/** Consumes real files produced by the current Studio Core serializers. */
public final class CurrentResourceIdentityProbe {

    private CurrentResourceIdentityProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args[0]);
        byte[] project = Files.readAllBytes(root.resolve("project.json"));
        ProjectRepository.readPackagedProject(project, "project.json");
        if (!new ProjectRepository(root.toFile()).reload()
            .isSuccessful()) throw new AssertionError("Complete runtime Project reload failed");
        Set<String> actors = new HashSet<String>();
        Set<String> items = new HashSet<String>();
        Set<String> groups = new HashSet<String>();
        try (Stream<Path> files = Files.walk(root)) {
            for (Path file : (Iterable<Path>) files.filter(
                path -> Files.isRegularFile(path) && path.toString()
                    .endsWith(".json"))::iterator) {
                String path = root.relativize(file)
                    .toString()
                    .replace('\\', '/');
                byte[] bytes = Files.readAllBytes(file);
                if (path.startsWith("actors/")) actors.add(
                    ProjectRepository.readPackagedActor(bytes, path)
                        .getId());
                else if (path.startsWith("items/")) items.add(
                    ProjectRepository.readPackagedItem(bytes, path, "individual")
                        .getId());
                else if (path.startsWith("item_groups/")) groups.add(
                    ProjectRepository.readPackagedItem(bytes, path, "collective")
                        .getId());
            }
        }
        CanonicalProjectContent content = new CanonicalProjectContentLoader().load(root, actors, items, groups);
        if (content.getStories()
            .size() != 2 || actors.size() != 1
            || items.size() != 1
            || groups.size() != 1) throw new AssertionError("Studio fixture contents were not completely loaded");
        String dynamic = new String(Files.readAllBytes(root.resolve("dynamic.txt")), StandardCharsets.UTF_8);
        String resolved = DynamicContentText.resolve(dynamic, (type, id) -> {
            if (!actors.contains(id)) throw new AssertionError("Dynamic actor owner lost");
            return "resolved";
        });
        if (!resolved.endsWith("resolved")) throw new AssertionError("Dynamic text did not resolve");
        JsonObject old = new JsonParser().parse(new String(project, StandardCharsets.UTF_8))
            .getAsJsonObject();
        old.addProperty("schema_version", 2);
        try {
            ProjectRepository.readPackagedProject(
                old.toString()
                    .getBytes(StandardCharsets.UTF_8),
                "project.json");
            throw new AssertionError("Legacy project was accepted");
        } catch (darkgrey.rpg.project.ProjectLoadException expected) {
            // Rejection is the required compatibility boundary.
        }
        darkgrey.rpg.project.packages.StoryPackageLoader packages = new darkgrey.rpg.project.packages.StoryPackageLoader(
            root.resolve("Packages")
                .toFile(),
            root.resolve("PackageCache")
                .toFile());
        darkgrey.rpg.project.packages.StoryPackageLoader.ReloadResult packageResult = packages.reload();
        if (packages.getPackages()
            .size() != 1) throw new AssertionError("Current Studio ZIP load failed: " + packageResult.getErrors());
        persistedBindings();
        System.out.println(
            "PASS: Studio-produced project, 2 Stories, Actor, Item, ItemGroup, Session, Task, memberships, dynamic reference and structured world bindings.");
    }

    private static void persistedBindings() {
        StoryUid owner = StoryUid.parse("ST-2345-6789-ABCD-EFGH");
        String actor = new ResourceAddress(owner, ResourceAddress.Kind.ACTOR, "actor").toKey();
        String group = new ResourceAddress(owner, ResourceAddress.Kind.ACTOR, "group").toKey();
        String item = new ResourceAddress(owner, ResourceAddress.Kind.ITEM, "item").toKey();
        UUID uuid = UUID.randomUUID();
        NpcIdentitySavedData npc = new NpcIdentitySavedData();
        npc.bind(actor, new NpcHostIdentity(uuid, "minecraft:villager", 0, null));
        NBTTagCompound npcNbt = new NBTTagCompound();
        npc.writeToNBT(npcNbt);
        NpcIdentitySavedData npcReload = new NpcIdentitySavedData();
        npcReload.readFromNBT(npcNbt);
        if (!actor.equals(npcReload.getNpcId(uuid))) throw new AssertionError("NPC owner lost");
        NBTTagCompound old = (NBTTagCompound) npcNbt.copy();
        old.setInteger("schema_version", 1);
        reject(() -> npcReload.readFromNBT(old));
        NBTTagCompound wrongList = (NBTTagCompound) npcNbt.copy();
        NBTTagList strings = new NBTTagList();
        strings.appendTag(new NBTTagString(actor));
        wrongList.setTag("bindings", strings);
        reject(() -> npcReload.readFromNBT(wrongList));
        if (!actor.equals(npcReload.getNpcId(uuid)))
            throw new AssertionError("Rejected NPC data mutated live bindings");
        ItemIdentitySavedData itemData = new ItemIdentitySavedData();
        itemData.bindItem(item, new ItemStackDefinition("examplemod:sample", 2, null));
        NBTTagCompound itemNbt = new NBTTagCompound();
        itemData.writeToNBT(itemNbt);
        ItemIdentitySavedData itemReload = new ItemIdentitySavedData();
        itemReload.readFromNBT(itemNbt);
        if (!"examplemod:sample".equals(
            itemReload.getItem(item)
                .getRegistryName()))
            throw new AssertionError("Mod registry name changed");
        NominatorSavedData selections = new NominatorSavedData();
        selections.put(new NominatorEntityBinding(uuid, actor, Collections.singletonList(group), owner.getValue()));
        selections.addTypeGroup("examplemod:entity", group);
        NBTTagCompound selectionNbt = new NBTTagCompound();
        selections.writeToNBT(selectionNbt);
        NominatorSavedData selectionReload = new NominatorSavedData();
        selectionReload.readFromNBT(selectionNbt);
        if (!actor.equals(
            selectionReload.get(uuid)
                .getIndividualId())
            || !selectionReload.getTypeGroups("examplemod:entity")
                .contains(group))
            throw new AssertionError("Selection identity lost");
        NBTTagCompound badSelection = (NBTTagCompound) selectionNbt.copy();
        badSelection.getTagList("type_groups", 10)
            .getCompoundTagAt(0)
            .setTag("groups", strings);
        reject(() -> selectionReload.readFromNBT(badSelection));
        if (!actor.equals(
            selectionReload.get(uuid)
                .getIndividualId()))
            throw new AssertionError("Rejected selection changed active bindings");
    }

    private static void reject(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Invalid persistence was accepted");
        } catch (IllegalArgumentException expected) {}
    }
}
