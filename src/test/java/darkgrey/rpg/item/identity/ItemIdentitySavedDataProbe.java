package darkgrey.rpg.item.identity;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

/** Offline proof for exact/fuzzy Item identities, multi-Group membership, and restart. */
public final class ItemIdentitySavedDataProbe {

    private ItemIdentitySavedDataProbe() {}

    public static void main(String[] args) {
        ItemIdentitySavedData data = ItemIdentitySavedData.get(new MapStorage(null));
        NBTTagCompound named = new NBTTagCompound();
        named.setString("display", "Royal Key");
        ItemStackDefinition key = new ItemStackDefinition("minecraft:tripwire_hook", 0, named);
        require(data.bindItem("royal_key", key), "bind exact Item ID");
        require(!data.bindItem("royal_key", key), "idempotent Item ID bind");
        require(data.bindItem("Team:Token", key), "bind uppercase local Item ID");
        require(data.bindItem("Team:token", key), "bind case-distinct Item ID");
        reject(new Runnable() {

            @Override
            public void run() {
                data.bindItem("royal_key", new ItemStackDefinition("minecraft:stick", 0, null));
            }
        }, "conflicting Item ID");

        ItemGroupMember iron = new ItemGroupMember(
            ItemMatchMode.FUZZY,
            new ItemStackDefinition("minecraft:iron_sword", 0, null));
        ItemGroupMember gold = new ItemGroupMember(
            ItemMatchMode.FUZZY,
            new ItemStackDefinition("minecraft:golden_sword", 0, null));
        require(data.addGroupMember("sword", iron), "first fuzzy Group member");
        require(data.addGroupMember("sword", gold), "second fuzzy Group member");
        require(!data.addGroupMember("sword", iron), "duplicate Group member rejected");
        require(data.addGroupMember("weapon", iron), "same item in multiple Groups");
        require(data.addGroupMember("Team:Tokens", iron), "bind uppercase local Item Group ID");
        require(data.addGroupMember("Team:tokens", iron), "bind case-distinct Item Group ID");

        NBTTagCompound checkpoint = new NBTTagCompound();
        data.writeToNBT(checkpoint);
        ItemIdentitySavedData restarted = new ItemIdentitySavedData();
        restarted.readFromNBT(checkpoint);
        NBTTagCompound deterministic = new NBTTagCompound();
        restarted.writeToNBT(deterministic);
        require(checkpoint.equals(deterministic), "deterministic restart");
        require(
            restarted.getItem("royal_key")
                .equals(key),
            "exact Item ID restart");
        require(
            restarted.getItem("Team:Token")
                .equals(key),
            "uppercase local Item ID restart");
        require(
            restarted.getItem("Team:token")
                .equals(key),
            "case-distinct Item ID restart");
        require(
            restarted.getGroup("sword")
                .size() == 2,
            "multi-member Group restart");
        require(
            restarted.getGroup("weapon")
                .size() == 1,
            "multi-Group restart");
        require(
            restarted.getGroup("Team:Tokens")
                .size() == 1,
            "uppercase local Item Group restart");
        require(
            restarted.getGroup("Team:tokens")
                .size() == 1,
            "case-distinct Item Group restart");

        NBTTagCompound unknown = (NBTTagCompound) checkpoint.copy();
        unknown.setString("unknown", "reject");
        rejectRead(unknown, "unknown root key");
        System.out.println("ITEM_IDENTITY_EXACT_ROUNDTRIP=PASS");
        System.out.println("ITEM_GROUP_EXACT_FUZZY_MODEL=PASS");
        System.out.println("ITEM_MULTI_GROUP_RESTART=PASS");
        System.out.println("ITEM_GROUP_CASE_SENSITIVE_RESTART=PASS");
    }

    private static void rejectRead(final NBTTagCompound value, String label) {
        reject(new Runnable() {

            @Override
            public void run() {
                new ItemIdentitySavedData().readFromNBT(value);
            }
        }, label);
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
            throw new AssertionError("Expected rejection: " + label);
        } catch (IllegalArgumentException expected) {
            // expected
        } catch (IllegalStateException expected) {
            // expected
        }
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }
}
