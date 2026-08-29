package darkgrey.rpg.player;

import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

/** Offline proof for UUID isolation, facts/choices/reward receipts, and restart. */
public final class PlayerRpgSavedDataProbe {

    private static final UUID A = UUID.fromString("00000000-0000-0000-0000-000000000201");
    private static final UUID B = UUID.fromString("00000000-0000-0000-0000-000000000202");

    private PlayerRpgSavedDataProbe() {}

    public static void main(String[] args) {
        MapStorage overworld = new MapStorage(null);
        PlayerRpgSavedData data = PlayerRpgSavedData.get(overworld);
        require(data == PlayerRpgSavedData.get(overworld), "overworld singleton");
        require(data.setFact(A, "forest", "princess_rescued", "true"), "fact set");
        require(data.recordChoice(A, "forest", "route", "rescue"), "choice set");
        require(data.claimReward(A, "forest", "action:royal_key"), "first reward claim");
        require(!data.claimReward(A, "forest", "action:royal_key"), "reward replay blocked");
        require(data.getFact(B, "forest", "princess_rescued") == null, "player B fact isolated");
        require(!data.hasClaimedReward(B, "forest", "action:royal_key"), "player B reward isolated");

        NBTTagCompound checkpoint = new NBTTagCompound();
        data.writeToNBT(checkpoint);
        PlayerRpgSavedData restarted = new PlayerRpgSavedData();
        restarted.readFromNBT(checkpoint);
        require("true".equals(restarted.getFact(A, "forest", "princess_rescued")), "fact restart");
        require("rescue".equals(restarted.getChoice(A, "forest", "route")), "choice restart");
        require(restarted.hasClaimedReward(A, "forest", "action:royal_key"), "reward receipt restart");
        NBTTagCompound deterministic = new NBTTagCompound();
        restarted.writeToNBT(deterministic);
        require(checkpoint.equals(deterministic), "deterministic restart");

        NBTTagCompound unknown = (NBTTagCompound) checkpoint.copy();
        unknown.setString("unknown", "reject");
        rejectRead(unknown);
        System.out.println("PLAYER_RPG_UUID_ISOLATION=PASS");
        System.out.println("PLAYER_RPG_FACT_CHOICE_REWARD_RESTART=PASS");
        System.out.println("PLAYER_RPG_OVERWORLD_SCOPE=PASS");
    }

    private static void rejectRead(NBTTagCompound value) {
        try {
            new PlayerRpgSavedData().readFromNBT(value);
            throw new AssertionError("Expected strict NBT rejection.");
        } catch (IllegalArgumentException expected) {}
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }
}
