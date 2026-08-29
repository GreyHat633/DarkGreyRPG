package darkgrey.rpg.entitytools;

import java.util.Arrays;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Focused pure-core probe for Stage 5 Copier and Storage Box semantics. */
public final class EntityToolsStage5Probe {

    private static final UUID SOURCE = UUID.fromString("11111111-1111-1111-1111-111111111111");
    private static final UUID RESTORED = UUID.fromString("22222222-2222-2222-2222-222222222222");

    public static void main(String[] args) {
        EntityCapture capture = capture(SOURCE, "customnpcs:npc");
        sanitizer();
        copier(capture);
        storage(capture);
        rejection(capture);
        System.out.println("ENTITY_TOOLS_STAGE5_PROBE=PASS");
    }

    private static void sanitizer() {
        NBTTagCompound source = new NBTTagCompound();
        source.setString("NpcId", "cnpc-root");
        source.setString("ActorId", "actor-root");
        source.setString("EntityId", "entity-root");
        source.setString("UniqueId", "unique-root");
        source.setString("UUID", SOURCE.toString());
        source.setString("Pos", "1,2,3");
        source.setString("Motion", "0,0,0");
        source.setString("Rotation", "0,0");
        source.setInteger("Dimension", 7);
        source.setString("World", "overworld");
        source.setString("dgr_npc_id", "dgr-root");
        source.setString("darkgrey_rpg.actor_id", "dgr-actor-root");

        NBTTagCompound nested = new NBTTagCompound();
        nested.setString("NpcId", "cnpc-nested");
        nested.setString("EntityId", "entity-nested");
        nested.setString("UUIDMost", "111");
        nested.setString("custom_tag", "kept");
        NBTTagList entries = new NBTTagList();
        NBTTagCompound entry = new NBTTagCompound();
        entry.setString("ActorId", "actor-list");
        entry.setString("UniqueId", "unique-list");
        entry.setString("Motion", "1,1,1");
        NBTTagList deep = new NBTTagList();
        NBTTagCompound deepEntry = new NBTTagCompound();
        deepEntry.setString("NpcId", "cnpc-deep");
        deepEntry.setString("dgr_unique_id", "dgr-deep");
        deep.appendTag(deepEntry);
        entry.setTag("deep", deep);
        entries.appendTag(entry);
        nested.setTag("entries", entries);
        source.setTag("cnpc_data", nested);

        NBTTagCompound sanitized = EntityTemplateSanitizer.sanitize(source);
        require("cnpc-root".equals(sanitized.getString("NpcId")), "root CNPC NpcId preserved");
        require("actor-root".equals(sanitized.getString("ActorId")), "root CNPC ActorId preserved");
        require("entity-root".equals(sanitized.getString("EntityId")), "root CNPC EntityId preserved");
        require("unique-root".equals(sanitized.getString("UniqueId")), "root CNPC UniqueId preserved");
        requireAbsent(
            sanitized,
            "UUID",
            "Pos",
            "Motion",
            "Rotation",
            "Dimension",
            "World",
            "dgr_npc_id",
            "darkgrey_rpg.actor_id");

        NBTTagCompound sanitizedNested = sanitized.getCompoundTag("cnpc_data");
        require("cnpc-nested".equals(sanitizedNested.getString("NpcId")), "nested CNPC NpcId preserved");
        require("entity-nested".equals(sanitizedNested.getString("EntityId")), "nested CNPC EntityId preserved");
        requireAbsent(sanitizedNested, "UUIDMost");
        NBTTagCompound sanitizedEntry = sanitizedNested.getTagList("entries", 10)
            .getCompoundTagAt(0);
        require("actor-list".equals(sanitizedEntry.getString("ActorId")), "list CNPC ActorId preserved");
        require("unique-list".equals(sanitizedEntry.getString("UniqueId")), "list CNPC UniqueId preserved");
        requireAbsent(sanitizedEntry, "Motion");
        NBTTagCompound sanitizedDeep = sanitizedEntry.getTagList("deep", 10)
            .getCompoundTagAt(0);
        require("cnpc-deep".equals(sanitizedDeep.getString("NpcId")), "deep CNPC NpcId preserved");
        requireAbsent(sanitizedDeep, "dgr_unique_id");
        EntityTemplateSanitizer.requireSanitized(sanitized);
        require(source.hasKey("UUID") && source.hasKey("dgr_npc_id"), "sanitizer does not mutate source");
    }

    private static void requireAbsent(NBTTagCompound value, String... keys) {
        for (String key : keys) require(!value.hasKey(key), "sanitizer retained '" + key + "'");
    }

    private static void copier(EntityCapture capture) {
        CopierState source = new CopierState();
        source.capture(capture);
        source.capture(
            new EntityCapture(
                UUID.randomUUID(),
                "minecraft:zombie",
                0,
                true,
                false,
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                null,
                Arrays.asList("undead")));
        source.select(1);
        NBTTagCompound saved = source.writeToNBT();
        CopierState restored = new CopierState();
        restored.readFromNBT(saved);
        require(
            restored.getTemplates()
                .size() == 2 && restored.getSelectedIndex() == 1,
            "copier templates/selection");
        EntitySpawnSpec copy = restored.copySelected(RESTORED);
        require(RESTORED.equals(copy.getUuid()) && !copy.hasEffectiveNpcId(), "copier fresh identity");
        require(
            copy.getGroups()
                .contains("undead"),
            "copier group inheritance");
        require(
            !copy.getConfiguration()
                .hasKey("UUID")
                && !copy.getConfiguration()
                    .hasKey("Pos"),
            "copier strips transient keys");
        restored.remove(0);
        require(
            restored.getTemplates()
                .size() == 1 && restored.getSelectedIndex() == 0,
            "copier template deletion keeps a valid selection");
        restored.remove(0);
        require(
            restored.getTemplates()
                .isEmpty() && restored.getSelectedIndex() == -1,
            "copier final deletion clears selection");
        NBTTagCompound malformed = (NBTTagCompound) saved.copy();
        malformed.setString("unexpected", "reject");
        rejectCopier(malformed);
    }

    private static void storage(EntityCapture capture) {
        StorageBoxState survival = new StorageBoxState();
        StoragePayload captured = survival.capture(capture, StorageMode.SURVIVAL, "boss");
        require(captured.isIdentityReserved(), "survival reserves identity");
        EntitySpawnSpec restored = survival.release();
        require(
            SOURCE.equals(restored.getUuid()) && "boss".equals(restored.getReservedNpcId()),
            "survival restore identity");
        require(!survival.isOccupied(), "survival empties after release");

        StorageBoxState creative = new StorageBoxState();
        creative.capture(capture, StorageMode.CREATIVE, "ignored");
        EntitySpawnSpec one = creative.release();
        EntitySpawnSpec two = creative.release();
        require(
            !one.getUuid()
                .equals(two.getUuid()) && !one.hasEffectiveNpcId() && creative.isOccupied(),
            "creative unlimited fresh copies");
        require(
            one.getGroups()
                .contains("guards"),
            "creative group inheritance");
        StorageBoxState restarted = new StorageBoxState();
        restarted.readFromNBT(creative.writeToNBT());
        require(restarted.isOccupied(), "creative persistence");
    }

    private static void rejection(EntityCapture capture) {
        rejectStorage(
            new EntityCapture(
                UUID.randomUUID(),
                "minecraft:item",
                0,
                false,
                false,
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                null,
                null));
        rejectStorage(
            new EntityCapture(
                UUID.randomUUID(),
                "minecraft:player",
                0,
                true,
                true,
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                new NBTTagCompound(),
                null,
                null));
    }

    private static EntityCapture capture(UUID uuid, String type) {
        NBTTagCompound configuration = new NBTTagCompound();
        configuration.setString("UUID", uuid.toString());
        configuration.setString("Pos", "0,0,0");
        configuration.setString("custom", "kept");
        return new EntityCapture(
            uuid,
            type,
            7,
            true,
            false,
            configuration,
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            new NBTTagCompound(),
            "boss",
            Arrays.asList("guards", "guards"));
    }

    private static void rejectStorage(EntityCapture capture) {
        try {
            new StorageBoxState().capture(capture, StorageMode.SURVIVAL, null);
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("unsupported storage target accepted");
    }

    private static void rejectCopier(NBTTagCompound value) {
        try {
            new CopierState().readFromNBT(value);
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("malformed copier payload accepted");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
