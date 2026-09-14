package darkgrey.rpg.task.forge;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.WeakHashMap;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.storage.SaveHandler;

import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskTransactionJournal;
import darkgrey.rpg.task.runtime.CanonicalTaskRewardPackage;

/** Server-thread inventory/XP transactions shared by Task rewards and submissions. */
public final class CanonicalTaskPlayerTransactions {

    private static final String RECEIPTS = "DGRTaskReceipts";
    private static final Map<EntityPlayerMP, Boolean> RECOVERED = Collections
        .synchronizedMap(new WeakHashMap<EntityPlayerMP, Boolean>());

    private CanonicalTaskPlayerTransactions() {}

    public static void recover(EntityPlayerMP player) {
        if (RECOVERED.containsKey(player)) return;
        try {
            journal(player).recover(player.getUniqueID(), state(player));
            RECOVERED.put(player, Boolean.TRUE);
        } catch (IOException | RuntimeException failure) {
            failClosed(player, failure);
        }
    }

    public static String key(CanonicalTaskInstanceSnapshot task, String nodeId, String kind) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            for (String part : new String[] { kind, task.getPlayerUuid()
                .toString(), task.getStoryInstanceId(), task.getTaskNodePlacementId(),
                String.valueOf(task.getActivationTime()), task.getRuntimeSnapshot()
                    .getResourceFingerprint(),
                nodeId }) {
                byte[] bytes = part.getBytes(StandardCharsets.UTF_8);
                digest.update((byte) (bytes.length >>> 24));
                digest.update((byte) (bytes.length >>> 16));
                digest.update((byte) (bytes.length >>> 8));
                digest.update((byte) bytes.length);
                digest.update(bytes);
            }
            StringBuilder result = new StringBuilder();
            for (byte value : digest.digest()) result.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
            return result.toString();
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException(impossible);
        }
    }

    public static boolean hasReceipt(EntityPlayerMP player, String key) {
        recover(player);
        return receipts(player).getBoolean(key);
    }

    public static void commit(EntityPlayerMP player, String key, NBTTagCompound image) {
        recover(player);
        try {
            NBTTagCompound receipt = (NBTTagCompound) receipts(player).copy();
            receipt.setBoolean(key, true);
            image.setTag("receipts", receipt);
            // Validate every slot and XP field before the durable commit point.
            validate(image, player.inventory.mainInventory.length);
            journal(player).commit(player.getUniqueID(), key, image, state(player));
        } catch (IOException | RuntimeException failure) {
            failClosed(player, failure);
        }
    }

    public static NBTTagCompound image(EntityPlayerMP player, ItemStack[] inventory) {
        NBTTagCompound image = new NBTTagCompound();
        NBTTagList slots = new NBTTagList();
        for (int i = 0; i < inventory.length; i++) if (inventory[i] != null) {
            NBTTagCompound slot = new NBTTagCompound();
            slot.setInteger("index", i);
            inventory[i].writeToNBT(slot);
            slots.appendTag(slot);
        }
        image.setTag("inventory", slots);
        image.setInteger("level", player.experienceLevel);
        image.setInteger("total", player.experienceTotal);
        image.setFloat("fraction", player.experience);
        image.setTag("receipts", receipts(player).copy());
        return image;
    }

    /** Null means insufficient room; the original player state has not changed. */
    public static NBTTagCompound rewardImage(EntityPlayerMP player, List<CanonicalTaskRewardPackage.Entry> entries) {
        recover(player);
        ItemStack[] inventory = CanonicalTaskInventory.copy(player.inventory.mainInventory);
        long xp = Math.min(
            Integer.MAX_VALUE,
            xpAtLevel(player.experienceLevel) + Math.round(player.experience * xpCapacity(player.experienceLevel)));
        for (CanonicalTaskRewardPackage.Entry entry : entries) {
            if ("xp".equals(entry.getType())) {
                xp = Math.max(0L, Math.min(Integer.MAX_VALUE, xp + entry.getAmount()));
                continue;
            }
            ItemStackDefinition definition = ItemIdentitySavedData.get()
                .getItem(entry.getItem());
            if (definition == null) throw new IllegalStateException("Unknown Task reward item: " + entry.getItem());
            if (!applyItem(inventory, definition.createStack(1), entry.getAmount())) return null;
        }
        NBTTagCompound image = image(player, inventory);
        setXp(image, (int) xp);
        return image;
    }

    /** Signed deltas; removals clamp at zero and additions never drop into the world. */
    public static boolean applyItem(ItemStack[] inventory, ItemStack prototype, int amount) {
        if (prototype == null || prototype.getItem() == null)
            throw new IllegalArgumentException("Reward item required.");
        long remaining = Math.abs((long) amount);
        for (int i = 0; i < inventory.length && remaining > 0; i++) {
            ItemStack stack = inventory[i];
            if (stack == null || stack.getItem() != prototype.getItem()
                || stack.getItemDamage() != prototype.getItemDamage()
                || !ItemStack.areItemStackTagsEqual(stack, prototype)) continue;
            int delta = (int) Math.min(
                remaining,
                amount < 0 ? stack.stackSize : Math.max(0, Math.min(64, stack.getMaxStackSize()) - stack.stackSize));
            stack.stackSize += amount < 0 ? -delta : delta;
            remaining -= delta;
            if (stack.stackSize == 0) inventory[i] = null;
        }
        if (amount < 0) return true;
        for (int i = 0; i < inventory.length && remaining > 0; i++) if (inventory[i] == null) {
            int delta = (int) Math.min(remaining, Math.min(64, prototype.getMaxStackSize()));
            inventory[i] = prototype.copy();
            inventory[i].stackSize = delta;
            remaining -= delta;
        }
        return remaining == 0;
    }

    public static void setXp(NBTTagCompound image, int total) {
        if (total < 0) throw new IllegalArgumentException("XP must be nonnegative.");
        int low = 0, high = 100000;
        while (low < high) {
            int middle = (low + high + 1) / 2;
            if (xpAtLevel(middle) <= total) low = middle;
            else high = middle - 1;
        }
        image.setInteger("total", total);
        image.setInteger("level", low);
        image.setFloat("fraction", (float) (total - xpAtLevel(low)) / xpCapacity(low));
    }

    /** Same XP-point arithmetic for instant Story actions, without a Task receipt. */
    public static void applyXpDelta(EntityPlayerMP player, int delta) {
        if (delta == 0) return;
        long current = xpAtLevel(player.experienceLevel)
            + Math.round(player.experience * xpCapacity(player.experienceLevel));
        NBTTagCompound result = new NBTTagCompound();
        setXp(result, (int) Math.max(0L, Math.min(Integer.MAX_VALUE, current + delta)));
        player.experienceLevel = result.getInteger("level");
        player.experienceTotal = result.getInteger("total");
        player.experience = result.getFloat("fraction");
    }

    private static long xpAtLevel(int level) {
        if (level < 15) return 17L * level;
        if (level < 30) {
            long n = level - 15;
            return 255 + 17 * n + 3 * n * (n - 1) / 2;
        }
        long n = level - 30;
        return 825 + 62 * n + 7 * n * (n - 1) / 2;
    }

    private static int xpCapacity(int level) {
        return level >= 30 ? 62 + (level - 30) * 7 : level >= 15 ? 17 + (level - 15) * 3 : 17;
    }

    private static NBTTagCompound receipts(EntityPlayerMP player) {
        return player.getEntityData()
            .getCompoundTag(EntityPlayer.PERSISTED_NBT_TAG)
            .getCompoundTag(RECEIPTS);
    }

    private static CanonicalTaskTransactionJournal journal(EntityPlayerMP player) {
        if (!(player.worldObj.getSaveHandler() instanceof SaveHandler))
            throw new IllegalStateException("Task transactions require a verifiable player save handler.");
        return new CanonicalTaskTransactionJournal(
            player.worldObj.getSaveHandler()
                .getWorldDirectory()
                .toPath()
                .resolve("data")
                .resolve("dgr_task_transactions"));
    }

    private static CanonicalTaskTransactionJournal.PlayerState state(final EntityPlayerMP player) {
        return new CanonicalTaskTransactionJournal.PlayerState() {

            @Override
            public boolean hasReceipt(String receipt) {
                return receipts(player).getBoolean(receipt);
            }

            @Override
            public void apply(NBTTagCompound image) {
                ItemStack[] inventory = validate(image, player.inventory.mainInventory.length);
                System.arraycopy(inventory, 0, player.inventory.mainInventory, 0, inventory.length);
                player.experienceLevel = image.getInteger("level");
                player.experienceTotal = image.getInteger("total");
                player.experience = image.getFloat("fraction");
                NBTTagCompound persisted = player.getEntityData()
                    .getCompoundTag(EntityPlayer.PERSISTED_NBT_TAG);
                persisted.setTag(
                    RECEIPTS,
                    image.getCompoundTag("receipts")
                        .copy());
                player.getEntityData()
                    .setTag(EntityPlayer.PERSISTED_NBT_TAG, persisted);
                player.inventory.markDirty();
                player.inventoryContainer.detectAndSendChanges();
            }

            @Override
            public void checkpoint() throws IOException {
                SaveHandler handler = (SaveHandler) player.worldObj.getSaveHandler();
                handler.writePlayerData(player);
                NBTTagCompound saved = handler.getPlayerNBT(player);
                if (saved == null || !receipts(player).equals(
                    saved.getCompoundTag("ForgeData")
                        .getCompoundTag(EntityPlayer.PERSISTED_NBT_TAG)
                        .getCompoundTag(RECEIPTS)))
                    throw new IOException("Task player checkpoint did not persist receipts.");
            }
        };
    }

    private static ItemStack[] validate(NBTTagCompound image, int size) {
        if (!image.hasKey("inventory", 9) || !image.hasKey("receipts", 10)
            || !image.hasKey("level", 3)
            || !image.hasKey("total", 3)
            || !image.hasKey("fraction", 5)
            || image.getInteger("level") < 0
            || image.getInteger("total") < 0
            || !Float.isFinite(image.getFloat("fraction"))
            || image.getFloat("fraction") < 0
            || image.getFloat("fraction") >= 1) throw new IllegalArgumentException("Invalid Task player after-image.");
        NBTTagList slots = image.getTagList("inventory", 10);
        if (((NBTTagList) image.getTag("inventory")).tagCount() != slots.tagCount())
            throw new IllegalArgumentException("Invalid Task inventory list type.");
        NBTTagCompound receipts = image.getCompoundTag("receipts");
        for (Object key : receipts.func_150296_c())
            if (!(key instanceof String) || !((String) key).matches("[0-9a-f]{64}")
                || !receipts.hasKey((String) key, 1)
                || receipts.getByte((String) key) != 1)
                throw new IllegalArgumentException("Invalid Task player receipt.");
        ItemStack[] inventory = new ItemStack[size];
        for (int i = 0; i < slots.tagCount(); i++) {
            NBTTagCompound slot = slots.getCompoundTagAt(i);
            int index = slot.getInteger("index");
            ItemStack stack = ItemStack.loadItemStackFromNBT(slot);
            if (!slot.hasKey("index", 3) || index < 0
                || index >= size
                || inventory[index] != null
                || stack == null
                || stack.stackSize <= 0) throw new IllegalArgumentException("Invalid Task inventory slot.");
            inventory[index] = stack;
        }
        return inventory;
    }

    private static void failClosed(EntityPlayerMP player, Exception failure) {
        RECOVERED.remove(player);
        if (player.playerNetServerHandler != null)
            player.playerNetServerHandler.kickPlayerFromServer("任务物品事务保存失败，请重新连接以恢复。");
        throw new IllegalStateException("Task transaction requires forward recovery.", failure);
    }
}
