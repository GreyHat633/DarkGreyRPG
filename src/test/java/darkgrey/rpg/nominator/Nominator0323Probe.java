package darkgrey.rpg.nominator;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;

/** Identity lifecycle, exact/fuzzy unbind, persistence and presentation source guards. */
public final class Nominator0323Probe {

    private Nominator0323Probe() {}

    public static void main(String[] args) throws Exception {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            "hero",
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_INDIVIDUAL,
                "hero",
                "Hero",
                "",
                Collections.<String>emptyList(),
                ""));
        actors.put(
            "group",
            new ActorDefinition(
                2,
                ActorDefinition.TYPE_COLLECTIVE,
                "group",
                "Group",
                "",
                Collections.<String>emptyList(),
                ""));
        ProjectSnapshot project = new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            darkgrey.rpg.graph.canonical.CanonicalProjectContent.empty());
        NpcIdentitySavedData npc = new NpcIdentitySavedData();
        NominatorSavedData selections = new NominatorSavedData();
        UUID a = UUID.randomUUID(), b = UUID.randomUUID();
        require(
            NominatorService
                .bindEntity(true, a, "Pig", 0, "hero", Arrays.asList("group"), null, project, npc, selections)
                .isAccepted(),
            "bind A");
        long rev = npc.getRevision();
        require(
            !NominatorService
                .bindEntity(true, b, "Pig", 0, "hero", Arrays.asList("group"), null, project, npc, selections)
                .isAccepted(),
            "normal conflict rejected");
        require(npc.getRevision() == rev && npc.getNpcId(b) == null, "conflict atomic");
        require(
            NominatorService
                .bindEntity(true, b, "Pig", 0, "hero", Arrays.asList("group"), null, true, project, npc, selections)
                .isAccepted(),
            "transfer B");
        require(npc.getNpcId(a) == null && "hero".equals(npc.getNpcId(b)), "reverse transfer");
        require(
            selections.get(a)
                .getIndividualId() == null && selections.get(a)
                    .getGroupIds()
                    .contains("group"),
            "old host groups preserved");
        require(
            !NominatorService.releaseEntityResource(false, "hero", project, npc, selections)
                .isAccepted(),
            "permission guard");
        require(
            NominatorService.releaseEntityResource(true, "hero", project, npc, selections)
                .isAccepted(),
            "orphan release without entity object");
        require(
            npc.getHost("hero") == null && selections.get(b)
                .getIndividualId() == null && project.getActor("hero") != null,
            "orphan metadata cleanup resource kept");
        require(
            "noop".equals(
                NominatorService.releaseEntityResource(true, "hero", project, npc, selections)
                    .getCode()),
            "already free typed noop");
        NominatorService.bindEntity(true, b, "Pig", 0, "hero", Arrays.asList("group"), null, project, npc, selections);
        NominatorService.unbindEntity(true, b, "Pig", 0, project, npc, selections);
        require(
            selections.get(b) == null && selections.get(a)
                .getGroupIds()
                .contains("group"),
            "host only unbind");
        selections.addTypeGroup("Pig", "group");
        NominatorService.releaseEntityResource(true, "group", project, npc, selections);
        require(
            selections.get(a) == null && selections.getTypeGroups("Pig")
                .isEmpty() && project.getActor("group") != null,
            "group world release");
        System.out.println("NPCID_ORPHAN_TRANSFER_HOST_UNBIND_GROUP_RELEASE=PASS");
        Item item = new Item();
        java.lang.reflect.Method register = Item.itemRegistry.getClass()
            .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
        register.setAccessible(true);
        register.invoke(Item.itemRegistry, 31000, "probe:token", item);
        ItemStack sa = new ItemStack(item, 3, 1), sb = new ItemStack(item, 1, 2);
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString("name", "A");
        sa.setTagCompound(tag);
        ItemIdentitySavedData data = new ItemIdentitySavedData();
        ItemStackDefinition da = ItemStackDefinition.capture(sa), db = ItemStackDefinition.capture(sb);
        data.bindItem("one", da);
        data.bindItem("two", da);
        data.addGroupMember("exact", new ItemGroupMember(ItemMatchMode.EXACT, da));
        data.addGroupMember("fuzzy", new ItemGroupMember(ItemMatchMode.FUZZY, da));
        data.addGroupMember("fuzzy", new ItemGroupMember(ItemMatchMode.EXACT, db));
        require(
            data.matchesGroup("exact", sa) && !data.matchesGroup("exact", sb) && data.matchesGroup("fuzzy", sb),
            "exact fuzzy metadata");
        ItemStack differentNbt = sa.copy();
        differentNbt.setTagCompound(null);
        require(!data.matchesGroup("exact", differentNbt) && data.matchesGroup("fuzzy", differentNbt), "NBT equality");
        ItemStack count = sa.copy();
        count.stackSize = 1;
        require(data.matchesGroup("exact", count), "count ignored");
        long before = data.getRevision();
        data.transferItem("one", db);
        require(
            data.getRevision() == before + 1 && data.matchesItem("one", sb) && data.matchesGroup("exact", sa),
            "atomic item transfer keeps groups");
        before = data.getRevision();
        require(data.unbindDefinition(sa), "unbind all applicable");
        require(
            data.getRevision() == before + 1 && !data.matchesItem("two", sa)
                && data.matchesItem("one", sb)
                && data.getGroup("exact")
                    .isEmpty()
                && data.getGroup("fuzzy")
                    .size() == 1,
            "single revision unrelated exact preserved fuzzy rule removed");
        require(
            sa.stackSize == 3 && sa.getTagCompound()
                .equals(tag),
            "physical stack unchanged");
        require(
            data.releaseGroup("fuzzy") && data.getGroup("fuzzy")
                .isEmpty(),
            "group release");
        NBTTagCompound saved = new NBTTagCompound();
        data.writeToNBT(saved);
        ItemIdentitySavedData loaded = new ItemIdentitySavedData();
        loaded.readFromNBT(saved);
        require(
            loaded.matchesItem("one", sb) && loaded.getGroup("fuzzy")
                .isEmpty(),
            "restart");
        require(loaded.unbindItem("one") && loaded.getItem("one") == null, "item release");
        System.out.println("ITEM_TRANSFER_EXACT_FUZZY_UNBIND_RELEASE_PERSISTENCE=PASS");
        for (String file : Arrays.asList(
            "GuiRpgButton",
            "GuiCanonicalStoryChooser",
            "CanonicalDialogueRenderer",
            "GuiCanonicalTaskScreen",
            "GuiCopierTemplates",
            "GuiNominatorInventory",
            "GuiNominatorEntity",
            "NominatorControls")) {
            String s = source("client/gui/" + file + ".java");
            require(s.contains("DgrUiPalette"), "shared palette " + file);
            require(
                !s.matches(
                    "(?s).*0x(?:FFE4D5AE|FF958976|EE40392E|FFF0CD|FFFFD27A|FF806C4E|DDCCAA|FF574C32|FFFFE8A8).*"),
                "warm literals " + file);
        }
        String browser = source("client/gui/NominatorBrowser.java").replaceAll("\\s+", "");
        require(browser.contains("resourceLabel(r,") && !browser.contains("top+20"), "one line resource");
        require(browser.contains("row.source.getDisplayName()"), "global provenance");
        String modal = source("client/gui/NominatorControls.java");
        require(
            modal.replaceAll("\\s+", "")
                .contains("left=newGuiRpgButton(90")
                && modal.replaceAll("\\s+", "")
                    .contains("right=newGuiRpgButton(91"),
            "modal button order");
        require(
            source("client/gui/GuiNominatorInventory.java").contains("UNBIND_SLOT")
                && !source("client/gui/GuiNominatorInventory.java").contains("player.closeScreen"),
            "two slot keep open");
        System.out.println("NOMINATOR_PALETTE_ROWS_MODAL_SOURCE_GUARD=PASS");
    }

    private static String source(String path) throws Exception {
        return new String(Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/" + path)), StandardCharsets.UTF_8);
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
