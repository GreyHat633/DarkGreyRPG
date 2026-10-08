package darkgrey.rpg.task.forge;

import java.util.Map;

import net.minecraft.item.ItemStack;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;

/** Uses existing server-owned Item/Group identity and exact Objective metadata. */
public final class CanonicalTaskInventory {

    private CanonicalTaskInventory() {}

    public static boolean matches(ItemStack stack, CanonicalGraphNode objective, ItemIdentitySavedData identities) {
        if (stack == null || stack.getItem() == null || stack.stackSize <= 0 || objective == null || identities == null)
            return false;
        JsonElement value = objective.getProperties()
            .get("item");
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            return false;
        String target = value.getAsString();
        if (!ResourceAddress.isKey(target)) return false;
        ResourceAddress.Kind kind = ResourceAddress.fromKey(target)
            .getKind();
        boolean identity = kind == ResourceAddress.Kind.ITEM && identities.matchingItemIds(stack)
            .contains(target) || kind == ResourceAddress.Kind.ITEM_GROUP
                && identities.matchingGroupIds(stack)
                    .contains(target);
        if (!identity) return false;
        JsonElement metadata = objective.getProperties()
            .get("metadata");
        if (metadata == null || !metadata.isJsonObject()) return false;
        for (Map.Entry<String, JsonElement> field : metadata.getAsJsonObject()
            .entrySet())
            if (!"damage".equals(field.getKey()) || !String.valueOf(stack.getItemDamage())
                .equals(
                    field.getValue()
                        .getAsString()))
                return false;
        return true;
    }

    public static int count(ItemStack[] inventory, CanonicalGraphNode objective, ItemIdentitySavedData identities) {
        long count = 0;
        for (ItemStack stack : inventory) if (matches(stack, objective, identities)) count += stack.stackSize;
        return (int) Math.min(Integer.MAX_VALUE, count);
    }

    public static ItemStack[] copy(ItemStack[] inventory) {
        ItemStack[] copy = new ItemStack[inventory.length];
        for (int i = 0; i < inventory.length; i++) copy[i] = inventory[i] == null ? null : inventory[i].copy();
        return copy;
    }

    public static boolean removeExact(ItemStack[] inventory, CanonicalGraphNode objective,
        ItemIdentitySavedData identities, int required) {
        if (required <= 0 || count(inventory, objective, identities) < required) return false;
        int remaining = required;
        for (int i = 0; i < inventory.length && remaining > 0; i++) if (matches(inventory[i], objective, identities)) {
            int removed = Math.min(remaining, inventory[i].stackSize);
            inventory[i].stackSize -= removed;
            remaining -= removed;
            if (inventory[i].stackSize == 0) inventory[i] = null;
        }
        return true;
    }
}
