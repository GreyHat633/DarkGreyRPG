package darkgrey.rpg.creator;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Set;

import net.minecraft.item.ItemStack;
import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;

/** Versioned bounded descriptor index. Stacks and full NBT rows exist only for the requested page. */
public final class TaskCandidateIndex {

    private static final LinkedHashMap<String, Index> CACHE = new LinkedHashMap<String, Index>(16, .75f, true);
    private static Object project;
    private static ItemIdentitySavedData owner;
    private static long bindingVersion = -1;
    private static int bytes;
    private static final int MAX_INDEX_BYTES = 4 * 1024 * 1024;

    private TaskCandidateIndex() {}

    private static Index index(CanonicalGraphNode node, ItemIdentitySavedData bindings) {
        Object current = DarkGreyRpg.getProjectRepository()
            .getSnapshot();
        if (current != project || bindings != owner || bindings.getRevision() != bindingVersion) {
            CACHE.clear();
            bytes = 0;
            project = current;
            owner = bindings;
            bindingVersion = bindings.getRevision();
        }
        String key = node.getId() + ":"
            + node.getProperties()
                .toString();
        Index found = CACHE.get(key);
        if (found != null) return found;
        String id = node.getProperties()
            .get("item")
            .getAsString();
        Index value = new Index();
        Set<Descriptor> seen = new HashSet<Descriptor>();
        if (!id.isEmpty()) {
            ResourceAddress.Kind kind = ResourceAddress.fromKey(id)
                .getKind();
            if (kind == ResourceAddress.Kind.ITEM_GROUP) {
                value.group = true;
                for (ItemGroupMember member : bindings.getGroup(id)) include(
                    value,
                    seen,
                    new Descriptor(member.getDefinition(), member.getMatchMode() == ItemMatchMode.FUZZY),
                    node,
                    bindings);
            } else if (kind == ResourceAddress.Kind.ITEM) {
                ItemStackDefinition single = bindings.getItem(id);
                if (single != null) include(value, seen, new Descriptor(single, false), node, bindings);
            } else throw new IllegalArgumentException("Task candidate target must be a DGR Item or Item Group.");
        }
        while (!CACHE.isEmpty() && (CACHE.size() >= 32 || bytes + value.bytes > MAX_INDEX_BYTES)) {
            Iterator<Index> iterator = CACHE.values()
                .iterator();
            bytes -= iterator.next().bytes;
            iterator.remove();
        }
        CACHE.put(key, value);
        bytes += value.bytes;
        return value;
    }

    private static void include(Index index, Set<Descriptor> seen, Descriptor value, CanonicalGraphNode node,
        ItemIdentitySavedData bindings) {
        if (!seen.add(value)) return;
        try {
            ItemStack stack = example(value, node);
            // The descriptor is already an authorized member of this target's binding.
            // Check metadata once without rescanning every registry member for each candidate.
            for (java.util.Map.Entry<String, com.google.gson.JsonElement> field : node.getProperties()
                .get("metadata")
                .getAsJsonObject()
                .entrySet())
                if (!"damage".equals(field.getKey()) || !String.valueOf(stack.getItemDamage())
                    .equals(
                        field.getValue()
                            .getAsString()))
                    return;
        } catch (RuntimeException missing) { /* Retain unavailable finite members as visible placeholders. */ }
        // Definitions are immutable references owned by the binding registry. This index
        // retains descriptors, not another copy of every member's NBT.
        int weight = 128;
        if (index.bytes + weight > MAX_INDEX_BYTES)
            throw new IllegalArgumentException("Candidate descriptor index exceeds bounded budget");
        index.values.add(value);
        index.bytes += weight;
    }

    private static ItemStack example(Descriptor value, CanonicalGraphNode node) {
        ItemStack stack = value.definition.createStack(1);
        if (value.fuzzy && node.getProperties()
            .get("metadata")
            .getAsJsonObject()
            .has("damage"))
            stack.setItemDamage(
                node.getProperties()
                    .get("metadata")
                    .getAsJsonObject()
                    .get("damage")
                    .getAsInt());
        return stack;
    }

    private static NBTTagCompound row(Descriptor value, CanonicalGraphNode node) {
        try {
            return TaskItemPreview
                .stack(example(value, node), value.fuzzy ? "允许同类物品；图标仅为示例，仍按任务条件检查" : "需匹配此物品的类型、耐久和附加数据");
        } catch (RuntimeException missing) {
            NBTTagCompound row = new NBTTagCompound();
            row.setString("error", "绑定物品已不可用");
            return row;
        }
    }

    public static synchronized NBTTagCompound summary(CanonicalGraphNode node, ItemIdentitySavedData bindings) {
        NBTTagCompound result = page(node, bindings, 0, 1);
        result.setLong("bindings", bindings.getRevision());
        result.setLong(
            "package_revision",
            DarkGreyRpg.getProjectRepository()
                .getSnapshotRevision());
        return result;
    }

    public static synchronized NBTTagCompound page(CanonicalGraphNode node, ItemIdentitySavedData bindings, int cursor,
        int limit) {
        Index index = index(node, bindings);
        if (cursor < 0 || cursor > index.values.size() || limit < 1 || limit > 20)
            throw new IllegalArgumentException("Invalid candidate cursor");
        NBTTagCompound result = new NBTTagCompound();
        NBTTagList rows = new NBTTagList();
        int size = 0, next = cursor;
        while (next < index.values.size() && rows.tagCount() < limit) {
            NBTTagCompound row = row(index.values.get(next), node);
            int weight = measured(row);
            if (rows.tagCount() > 0 && size + weight > 65536) break;
            if (weight > 16384) throw new IllegalArgumentException("Candidate row exceeds page budget");
            rows.appendTag(row);
            size += weight;
            next++;
        }
        if (index.values.isEmpty()) {
            NBTTagCompound missing = new NBTTagCompound();
            missing.setString("error", "物品未绑定、已缺失或没有符合条件的候选");
            rows.appendTag(missing);
        }
        result.setBoolean("group", index.group);
        result.setInteger("total", index.values.size());
        result.setInteger("cursor", cursor);
        result.setInteger("next", next);
        result.setTag("items", rows);
        return result;
    }

    public static int measured(NBTTagCompound row) {
        try {
            java.io.ByteArrayOutputStream raw = new java.io.ByteArrayOutputStream();
            CompressedStreamTools.write(row, new java.io.DataOutputStream(raw));
            return raw.size();
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Cannot measure task presentation", invalid);
        }
    }

    private static final class Index {

        boolean group;
        int bytes;
        final List<Descriptor> values = new ArrayList<Descriptor>();
    }

    private static final class Descriptor {

        final ItemStackDefinition definition;
        final boolean fuzzy;

        Descriptor(ItemStackDefinition definition, boolean fuzzy) {
            this.definition = definition;
            this.fuzzy = fuzzy;
        }

        @Override
        public boolean equals(Object other) {
            return other instanceof Descriptor && fuzzy == ((Descriptor) other).fuzzy
                && definition.equals(((Descriptor) other).definition);
        }

        @Override
        public int hashCode() {
            return definition.hashCode() * 31 + (fuzzy ? 1 : 0);
        }
    }
}
