package darkgrey.rpg.item.identity;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

import net.minecraft.item.ItemStack;

/** World-shared server-authoritative bindings for DGR Item IDs and Groups. */
public final class ItemIdentityRegistry {

    private static final Pattern ID = Pattern.compile("[a-z0-9][a-z0-9_-]*");
    private final Map<String, ItemStackDefinition> items = new LinkedHashMap<String, ItemStackDefinition>();
    private final Map<String, List<ItemGroupMember>> groups = new LinkedHashMap<String, List<ItemGroupMember>>();

    public synchronized boolean bindItem(String itemId, ItemStackDefinition definition) {
        String id = requireId(itemId, "Item ID");
        if (definition == null) throw new IllegalArgumentException("Item definition is required.");
        ItemStackDefinition existing = items.get(id);
        if (existing != null) {
            if (existing.equals(definition)) return false;
            throw new IllegalStateException("Item ID '" + id + "' already has a different exact definition.");
        }
        items.put(id, definition);
        return true;
    }

    public synchronized boolean unbindItem(String itemId) {
        return items.remove(requireId(itemId, "Item ID")) != null;
    }

    public synchronized ItemStackDefinition getItem(String itemId) {
        return items.get(requireId(itemId, "Item ID"));
    }

    public synchronized boolean matchesItem(String itemId, ItemStack stack) {
        ItemStackDefinition definition = getItem(itemId);
        return definition != null && definition.matchesExact(stack);
    }

    public synchronized boolean addGroupMember(String groupId, ItemGroupMember member) {
        String id = requireId(groupId, "Item Group ID");
        if (member == null) throw new IllegalArgumentException("Item Group member is required.");
        List<ItemGroupMember> members = groups.get(id);
        if (members == null) {
            members = new ArrayList<ItemGroupMember>();
            groups.put(id, members);
        }
        if (members.contains(member)) return false;
        members.add(member);
        return true;
    }

    public synchronized boolean removeGroupMember(String groupId, ItemGroupMember member) {
        String id = requireId(groupId, "Item Group ID");
        List<ItemGroupMember> members = groups.get(id);
        if (members == null || !members.remove(member)) return false;
        if (members.isEmpty()) groups.remove(id);
        return true;
    }

    public synchronized List<ItemGroupMember> getGroup(String groupId) {
        List<ItemGroupMember> members = groups.get(requireId(groupId, "Item Group ID"));
        return members == null ? Collections.<ItemGroupMember>emptyList()
            : Collections.unmodifiableList(new ArrayList<ItemGroupMember>(members));
    }

    public synchronized boolean matchesGroup(String groupId, ItemStack stack) {
        for (ItemGroupMember member : getGroup(groupId)) if (member.matches(stack)) return true;
        return false;
    }

    public synchronized List<ItemBinding> itemBindings() {
        List<ItemBinding> result = new ArrayList<ItemBinding>();
        for (Map.Entry<String, ItemStackDefinition> entry : items.entrySet())
            result.add(new ItemBinding(entry.getKey(), entry.getValue()));
        Collections.sort(result, new Comparator<ItemBinding>() {

            @Override
            public int compare(ItemBinding left, ItemBinding right) {
                return left.getItemId()
                    .compareTo(right.getItemId());
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized List<GroupBinding> groupBindings() {
        List<GroupBinding> result = new ArrayList<GroupBinding>();
        for (Map.Entry<String, List<ItemGroupMember>> entry : groups.entrySet())
            for (ItemGroupMember member : entry.getValue()) result.add(new GroupBinding(entry.getKey(), member));
        Collections.sort(result, new Comparator<GroupBinding>() {

            @Override
            public int compare(GroupBinding left, GroupBinding right) {
                int id = left.getGroupId()
                    .compareTo(right.getGroupId());
                if (id != 0) return id;
                int mode = left.getMember()
                    .getMatchMode()
                    .getJsonName()
                    .compareTo(
                        right.getMember()
                            .getMatchMode()
                            .getJsonName());
                if (mode != 0) return mode;
                ItemStackDefinition a = left.getMember()
                    .getDefinition();
                ItemStackDefinition b = right.getMember()
                    .getDefinition();
                int item = a.getRegistryName()
                    .compareTo(b.getRegistryName());
                return item != 0 ? item : Integer.compare(a.getDamage(), b.getDamage());
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized void replaceAll(List<ItemBinding> itemValues, List<GroupBinding> groupValues) {
        if (itemValues == null || groupValues == null)
            throw new IllegalArgumentException("Item identity bindings are required.");
        ItemIdentityRegistry candidate = new ItemIdentityRegistry();
        for (ItemBinding binding : itemValues) {
            if (binding == null || !candidate.bindItem(binding.getItemId(), binding.getDefinition()))
                throw new IllegalArgumentException("Duplicate persisted Item ID binding.");
        }
        for (GroupBinding binding : groupValues) {
            if (binding == null || !candidate.addGroupMember(binding.getGroupId(), binding.getMember()))
                throw new IllegalArgumentException("Duplicate persisted Item Group member.");
        }
        items.clear();
        groups.clear();
        for (ItemBinding binding : candidate.itemBindings()) items.put(binding.getItemId(), binding.getDefinition());
        for (GroupBinding binding : candidate.groupBindings())
            addGroupMember(binding.getGroupId(), binding.getMember());
    }

    private static String requireId(String value, String label) {
        if (value == null || !ID.matcher(value)
            .matches()) throw new IllegalArgumentException(label + " must match [a-z0-9][a-z0-9_-]*.");
        return value;
    }

    public static final class ItemBinding {

        private final String itemId;
        private final ItemStackDefinition definition;

        public ItemBinding(String itemId, ItemStackDefinition definition) {
            this.itemId = requireId(itemId, "Item ID");
            if (definition == null) throw new IllegalArgumentException("Item definition is required.");
            this.definition = definition;
        }

        public String getItemId() {
            return itemId;
        }

        public ItemStackDefinition getDefinition() {
            return definition;
        }
    }

    public static final class GroupBinding {

        private final String groupId;
        private final ItemGroupMember member;

        public GroupBinding(String groupId, ItemGroupMember member) {
            this.groupId = requireId(groupId, "Item Group ID");
            if (member == null) throw new IllegalArgumentException("Item Group member is required.");
            this.member = member;
        }

        public String getGroupId() {
            return groupId;
        }

        public ItemGroupMember getMember() {
            return member;
        }
    }
}
