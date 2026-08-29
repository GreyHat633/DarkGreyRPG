package darkgrey.rpg.item.identity;

import net.minecraft.item.ItemStack;

/** One exact or fuzzy captured member inside a DGR Item Group. */
public final class ItemGroupMember {

    private final ItemMatchMode matchMode;
    private final ItemStackDefinition definition;

    public ItemGroupMember(ItemMatchMode matchMode, ItemStackDefinition definition) {
        if (matchMode == null || definition == null)
            throw new IllegalArgumentException("Item Group match mode and definition are required.");
        this.matchMode = matchMode;
        this.definition = definition;
    }

    public ItemMatchMode getMatchMode() {
        return matchMode;
    }

    public ItemStackDefinition getDefinition() {
        return definition;
    }

    public boolean matches(ItemStack stack) {
        return matchMode == ItemMatchMode.EXACT ? definition.matchesExact(stack) : definition.matchesFuzzy(stack);
    }

    @Override
    public boolean equals(Object value) {
        if (this == value) return true;
        if (!(value instanceof ItemGroupMember)) return false;
        ItemGroupMember other = (ItemGroupMember) value;
        return matchMode == other.matchMode && definition.equals(other.definition);
    }

    @Override
    public int hashCode() {
        return 31 * matchMode.hashCode() + definition.hashCode();
    }
}
