package darkgrey.rpg.creator;

import java.util.Arrays;
import java.util.Collections;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.client.NominatorGlobalSearch;
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class CreatorUxProbe {

    private CreatorUxProbe() {}

    public static void main(String[] args) {
        UUID a = new UUID(1, 2), b = new UUID(3, 4);
        CreatorInspectSavedData data = new CreatorInspectSavedData();
        require(!data.enabled(a), "default off");
        require(data.toggle(a) && !data.enabled(b), "UUID isolation");
        NBTTagCompound saved = new NBTTagCompound();
        data.writeToNBT(saved);
        CreatorInspectSavedData restarted = new CreatorInspectSavedData();
        restarted.readFromNBT(saved);
        require(restarted.enabled(a) && !restarted.enabled(b), "restart ON");
        require(!restarted.toggle(a), "toggle off");
        restarted.writeToNBT(saved);
        data.readFromNBT(saved);
        require(!data.enabled(a), "restart OFF");
        for (boolean persistent : new boolean[] { false, true })
            for (boolean goggles : new boolean[] { false, true }) require(
                CreatorInspectSavedData.effective(persistent, goggles) == (persistent || goggles),
                "effective truth table");
        System.out.println("CREATOR_INSPECT_SAVED_DATA_PROBE=PASS");
        System.out.println("CREATOR_INSPECT_EFFECTIVE_STATE_PROBE=PASS");

        NominatorCatalog.PackageChoice p = new NominatorCatalog.PackageChoice(
            "A",
            "A:story",
            "Alpha",
            Arrays.asList("X:npc"),
            Arrays.asList("X:key"),
            Arrays.asList("X:keys"));
        NominatorCatalog.PackageChoice q = new NominatorCatalog.PackageChoice(
            "B",
            "B:story",
            "Beta",
            Arrays.asList("X:npc"),
            Arrays.asList("X:key"),
            Arrays.asList("X:keys"));
        NominatorCatalog catalog = new NominatorCatalog(
            Collections.<NominatorCatalog.Story>emptyList(),
            Arrays.asList(
                new NominatorCatalog.Actor("X:npc", "Keeper", "individual", "A:story", "", Arrays.asList("tavern"))),
            Arrays.asList(new NominatorCatalog.Item("X:key", "Silver", Arrays.asList("unlock"))),
            Arrays.asList(new NominatorCatalog.Item("X:keys", "Keys", Arrays.asList("unlock"))),
            Arrays.asList(p, q));
        for (String query : new String[] { "X:npc", "Keeper", "tavern" }) {
            java.util.List<NominatorGlobalSearch.Row> rows = NominatorGlobalSearch.search(catalog, "A", query, false);
            require(rows.size() == 2 && rows.get(1).source == q, "global package-scoped actor search");
        }
        require(
            NominatorGlobalSearch.search(catalog, "B", "", false)
                .size() == 1,
            "normal package browsing");
        require(
            NominatorGlobalSearch.search(catalog, "A", "unlock", true)
                .size() == 4,
            "unified item and group tags");
        require(
            NominatorGlobalSearch.search(catalog, "A", "no_match", true)
                .isEmpty(),
            "empty search");
        System.out.println("NOMINATOR_GLOBAL_SEARCH_PROBE=PASS");

        ItemIdentitySavedData items = new ItemIdentitySavedData();
        ItemStackDefinition definition = new ItemStackDefinition("minecraft:stick", 0, null);
        items.bindItem("X:key", definition);
        items.addGroupMember("X:keys", new ItemGroupMember(ItemMatchMode.EXACT, definition));
        items.addGroupMember("Y:tools", new ItemGroupMember(ItemMatchMode.FUZZY, definition));
        NBTTagCompound itemTag = new NBTTagCompound();
        items.writeToNBT(itemTag);
        ByteBuf bytes = Unpooled.buffer();
        new CreatorSnapshot(0, itemTag).toBytes(bytes);
        CreatorSnapshot decoded = new CreatorSnapshot();
        decoded.fromBytes(bytes);
        bytes.release();
        ItemIdentitySavedData client = new ItemIdentitySavedData();
        client.readFromNBT(decoded.data);
        require(
            client.getItem("X:key")
                .equals(definition)
                && client.getGroup("X:keys")
                    .size() == 1
                && client.getGroup("Y:tools")
                    .size() == 1,
            "matcher catalog survives transport with groups");
        require(client.getRevision() == items.getRevision(), "catalog revision survives transport");
        if (args.length > 0 && "live".equals(args[0])) {
            net.minecraft.item.ItemStack stick = new net.minecraft.item.ItemStack(net.minecraft.init.Items.stick);
            require(
                client.matchingItemIds(stick)
                    .contains("X:key"),
                "exact stack matching");
            require(
                client.matchingGroupIds(stick)
                    .size() == 2,
                "multiple group stack matches");
            require(
                client.matchingItemIds(new net.minecraft.item.ItemStack(net.minecraft.init.Items.apple))
                    .isEmpty(),
                "no match");
            stick.stackSize = 16;
            require(
                client.matchingItemIds(stick)
                    .contains("X:key"),
                "stack count not identity");
            System.out.println("ITEM_INSPECT_LIVE_MATCH_PROBE=PASS");
        }
        items.unbindItem("X:key");
        require(items.getRevision() != client.getRevision(), "revision invalidates old catalog");
        for (byte[] invalid : new byte[][] { {}, { 2, 0, 0, 0, 1, 0 }, { 0, 0 }, { 1, 0, 0, 99 }, { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 2, 0 }, { 0, 0, 0, 0, 1, 0 }, { 0, 0, 16, 0, 1, 0 } }) {
            ByteBuf malformed = Unpooled.wrappedBuffer(invalid);
            boolean failed = false;
            try {
                new CreatorSnapshot().fromBytes(malformed);
            } catch (RuntimeException expected) {
                failed = true;
            } finally {
                malformed.release();
            }
            require(failed, "malformed creator codec rejected");
        }
        NBTTagCompound oversized = new NBTTagCompound();
        oversized.setByteArray("large", new byte[2097153]);
        ByteBuf oversizedBuffer = Unpooled.buffer();
        boolean rejected = false;
        try {
            new CreatorSnapshot(0, oversized).toBytes(oversizedBuffer);
        } catch (RuntimeException expected) {
            rejected = true;
        } finally {
            oversizedBuffer.release();
        }
        require(rejected, "compressed payload cannot bypass decompressed size limit");
        ByteBuf requestBytes = Unpooled.buffer();
        new CanonicalTaskUiRequest(42).toBytes(requestBytes);
        CanonicalTaskUiRequest request = new CanonicalTaskUiRequest();
        request.fromBytes(requestBytes);
        requestBytes.release();
        require(request.request == 42, "task request correlation round trip");
        for (int length : new int[] { 0, 3, 5 }) {
            ByteBuf invalid = Unpooled.wrappedBuffer(new byte[length]);
            rejected = false;
            try {
                new CanonicalTaskUiRequest().fromBytes(invalid);
            } catch (RuntimeException expected) {
                rejected = true;
            } finally {
                invalid.release();
            }
            require(rejected, "task request exact width");
        }
        System.out.println("CREATOR_NETWORK_CODEC_PROBE=PASS");
        System.out.println("ITEM_INSPECT_CATALOG_PROBE=PASS");

        CanonicalTaskJournalObjectiveRow active = objective("first", CanonicalTaskObjectiveStatus.ACTIVE, 1);
        CanonicalTaskJournalObjectiveRow second = objective("second", CanonicalTaskObjectiveStatus.ACTIVE, 0);
        CanonicalTaskJournalObjectiveRow complete = objective("first", CanonicalTaskObjectiveStatus.COMPLETED, 3);
        NBTTagList tasks = CanonicalTaskUiProjection
            .project(
                Arrays.asList(
                    task("one", false, active, second),
                    task("two", false, active),
                    task("settled", true, complete)))
            .getTagList("tasks", 10);
        require(
            tasks.tagCount() == 2 && tasks.getCompoundTagAt(0)
                .getTagList("objectives", 10)
                .tagCount() == 2,
            "all active tasks and parallel objectives, settlement removed");
        tasks = CanonicalTaskUiProjection.project(Arrays.asList(task("one", false, complete, second)))
            .getTagList("tasks", 10);
        require(
            tasks.getCompoundTagAt(0)
                .getTagList("objectives", 10)
                .tagCount() == 1,
            "objective transition");
        tasks = CanonicalTaskUiProjection.project(Arrays.asList(task("one", false, complete)))
            .getTagList("tasks", 10);
        require(
            tasks.getCompoundTagAt(0)
                .getBoolean("complete"),
            "complete without settlement");
        System.out.println("CANONICAL_TASK_UI_PROJECTION_PROBE=PASS");
    }

    private static CanonicalTaskJournalObjectiveRow objective(String id, CanonicalTaskObjectiveStatus status,
        int progress) {
        return new CanonicalTaskJournalObjectiveRow(id, id, "kill", status, progress, 3, true, id);
    }

    private static CanonicalTaskJournalEntry task(String id, boolean settled,
        CanonicalTaskJournalObjectiveRow... rows) {
        return new CanonicalTaskJournalEntry(
            new UUID(1, 2),
            "story",
            id,
            "task",
            id,
            settled ? CanonicalTaskInstanceStatus.SETTLED : CanonicalTaskInstanceStatus.ACTIVE,
            1,
            settled ? Long.valueOf(2) : null,
            settled ? "done" : null,
            Collections.<String, Boolean>emptyMap(),
            Arrays.asList(rows));
    }

    private static void require(boolean value, String label) {
        if (!value) throw new IllegalStateException(label);
    }
}
