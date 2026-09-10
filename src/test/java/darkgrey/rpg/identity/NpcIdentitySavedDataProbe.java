package darkgrey.rpg.identity;

import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

/** Offline acceptance for unique identity conflicts, transfer, and restart. */
public final class NpcIdentitySavedDataProbe {

    private static final UUID FIRST = UUID.fromString("00000000-0000-0000-0000-000000000101");
    private static final UUID SECOND = UUID.fromString("00000000-0000-0000-0000-000000000102");
    private static final UUID THIRD = UUID.fromString("00000000-0000-0000-0000-000000000103");
    private static final UUID FOURTH = UUID.fromString("00000000-0000-0000-0000-000000000104");

    private NpcIdentitySavedDataProbe() {}

    public static void main(String[] args) {
        MapStorage storage = new MapStorage(null);
        NpcIdentitySavedData data = NpcIdentitySavedData.get(storage);
        require(data == NpcIdentitySavedData.get(storage), "MapStorage reuse");
        NpcHostIdentity wolf = new NpcHostIdentity(FIRST, "minecraft:wolf", 0);
        require(data.bind("tavern_boss", wolf), "initial bind");
        require(data.isDirty(), "bind dirty");
        data.setDirty(false);
        require(!data.bind("tavern_boss", wolf), "idempotent bind");
        require(!data.isDirty(), "idempotent clean");
        reject(new Runnable() {

            @Override
            public void run() {
                data.bind("tavern_boss", new NpcHostIdentity(SECOND, "minecraft:wolf", 0));
            }
        }, "duplicate NPC ID");
        reject(new Runnable() {

            @Override
            public void run() {
                data.bind("another", wolf);
            }
        }, "duplicate host");
        require(!data.isDirty(), "conflicts clean");

        require(
            data.bind("Team:Guard", new NpcHostIdentity(THIRD, "minecraft:wolf", 0)),
            "uppercase local NPC ID bind");
        require(data.bind("Team:guard", new NpcHostIdentity(FOURTH, "minecraft:wolf", 0)), "case-distinct NPC ID bind");
        require(data.getHost("Team:Guard") != null, "uppercase local NPC lookup");
        require(data.getHost("Team:guard") != null, "case-distinct NPC lookup");

        NBTTagCompound checkpoint = new NBTTagCompound();
        data.writeToNBT(checkpoint);
        require(
            !checkpoint.toString()
                .contains("darkgrey_rpg.actor_id"),
            "legacy stored-data key absent");
        NpcIdentitySavedData restarted = new NpcIdentitySavedData();
        restarted.readFromNBT(checkpoint);
        NBTTagCompound deterministic = new NBTTagCompound();
        restarted.writeToNBT(deterministic);
        require(checkpoint.equals(deterministic), "deterministic restart");
        require(
            restarted.getHost("tavern_boss")
                .getEntityUuid()
                .equals(FIRST),
            "restart lookup by ID");
        require("tavern_boss".equals(restarted.getNpcId(FIRST)), "restart lookup by host");
        require(restarted.getHost("Team:Guard") != null, "uppercase local NPC restart");
        require(restarted.getHost("Team:guard") != null, "case-distinct NPC restart");
        require(
            restarted.getHost("Team:Guard") != restarted.getHost("Team:guard"),
            "case-distinct NPC bindings collapsed");

        NpcHostIdentity revived = new NpcHostIdentity(SECOND, "customnpcs:customnpc", 0, "cnpc:owner");
        require(restarted.transfer("tavern_boss", revived), "explicit revival transfer");
        require(restarted.getNpcId(FIRST) == null, "old host released only by transfer");
        require("tavern_boss".equals(restarted.getNpcId(SECOND)), "new host owns identity");
        restarted.setDirty(false);
        require(!restarted.transfer("tavern_boss", revived), "idempotent transfer");
        require(!restarted.isDirty(), "idempotent transfer clean");
        require(restarted.unbindHost(SECOND), "explicit unbind");
        require(restarted.getHost("tavern_boss") == null, "unbind frees ID");

        NBTTagCompound unknown = (NBTTagCompound) checkpoint.copy();
        unknown.setString("unexpected", "reject");
        rejectRead(unknown, "unknown root key");
        NBTTagCompound duplicate = (NBTTagCompound) checkpoint.copy();
        net.minecraft.nbt.NBTTagList duplicateBindings = duplicate.getTagList("bindings", 10);
        duplicateBindings.appendTag(
            duplicateBindings.getCompoundTagAt(0)
                .copy());
        duplicate.setTag("bindings", duplicateBindings);
        rejectRead(duplicate, "duplicate persisted mapping");
        System.out.println("NPC_IDENTITY_EXTERNAL_REGISTRY=PASS");
        System.out.println("NPC_IDENTITY_UNIQUE_CONFLICT=PASS");
        System.out.println("NPC_IDENTITY_TRANSFER_RESTART=PASS");
        System.out.println("NPC_IDENTITY_CASE_SENSITIVE_RESTART=PASS");
    }

    private static void rejectRead(final NBTTagCompound value, String label) {
        reject(new Runnable() {

            @Override
            public void run() {
                new NpcIdentitySavedData().readFromNBT(value);
            }
        }, label);
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
            throw new AssertionError("Expected rejection: " + label);
        } catch (IllegalArgumentException expected) {
            // expected
        } catch (NpcIdentityConflictException expected) {
            // expected
        }
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }
}
