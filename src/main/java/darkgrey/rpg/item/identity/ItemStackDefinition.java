package darkgrey.rpg.item.identity;

import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;

/** Count-independent exact ItemStack identity captured by the server. */
public final class ItemStackDefinition {

    private final String registryName;
    private final int damage;
    private final NBTTagCompound tag;

    public ItemStackDefinition(String registryName, int damage, NBTTagCompound tag) {
        if (blank(registryName)) throw new IllegalArgumentException("Item registry name is required.");
        if (damage < 0) throw new IllegalArgumentException("Item damage cannot be negative.");
        this.registryName = registryName.trim();
        this.damage = damage;
        this.tag = tag == null ? null : (NBTTagCompound) tag.copy();
    }

    public static ItemStackDefinition capture(ItemStack stack) {
        if (stack == null || stack.getItem() == null) throw new IllegalArgumentException("ItemStack is required.");
        Object name = Item.itemRegistry.getNameForObject(stack.getItem());
        if (name == null) throw new IllegalArgumentException("ItemStack item is not registered.");
        return new ItemStackDefinition(String.valueOf(name), stack.getItemDamage(), stack.getTagCompound());
    }

    public String getRegistryName() {
        return registryName;
    }

    public int getDamage() {
        return damage;
    }

    public NBTTagCompound getTag() {
        return tag == null ? null : (NBTTagCompound) tag.copy();
    }

    public boolean matchesExact(ItemStack stack) {
        if (stack == null || stack.getItem() == null) return false;
        Object name = Item.itemRegistry.getNameForObject(stack.getItem());
        return name != null && registryName.equals(String.valueOf(name))
            && damage == stack.getItemDamage()
            && equalTag(tag, stack.getTagCompound());
    }

    public boolean matchesFuzzy(ItemStack stack) {
        if (stack == null || stack.getItem() == null) return false;
        Object name = Item.itemRegistry.getNameForObject(stack.getItem());
        return name != null && registryName.equals(String.valueOf(name));
    }

    public ItemStack createStack(int amount) {
        if (amount <= 0 || amount > 64) throw new IllegalArgumentException("Item amount must be between 1 and 64.");
        Object value = Item.itemRegistry.getObject(registryName);
        if (!(value instanceof Item))
            throw new IllegalStateException("Registered item is unavailable: " + registryName);
        ItemStack result = new ItemStack((Item) value, amount, damage);
        if (tag != null) result.setTagCompound((NBTTagCompound) tag.copy());
        return result;
    }

    @Override
    public boolean equals(Object value) {
        if (this == value) return true;
        if (!(value instanceof ItemStackDefinition)) return false;
        ItemStackDefinition other = (ItemStackDefinition) value;
        return damage == other.damage && registryName.equals(other.registryName) && equalTag(tag, other.tag);
    }

    @Override
    public int hashCode() {
        int result = registryName.hashCode();
        result = 31 * result + damage;
        return 31 * result + (tag == null ? 0
            : tag.toString()
                .hashCode());
    }

    private static boolean equalTag(NBTTagCompound left, NBTTagCompound right) {
        return left == null ? right == null : left.equals(right);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
