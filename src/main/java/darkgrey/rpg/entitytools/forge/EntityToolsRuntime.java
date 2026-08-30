package darkgrey.rpg.entitytools.forge;

import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.ItemStack;
import net.minecraft.util.ChatComponentText;
import net.minecraftforge.event.entity.player.EntityInteractEvent;
import net.minecraftforge.event.entity.player.PlayerInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.entitytools.CopierState;
import darkgrey.rpg.entitytools.EntityCapture;
import darkgrey.rpg.entitytools.EntitySpawnSpec;
import darkgrey.rpg.entitytools.StorageBoxState;
import darkgrey.rpg.entitytools.StorageMode;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.item.ItemStorageBox;

/** Server-authoritative Forge interaction bridge for Copier and Storage Box. */
public final class EntityToolsRuntime {

    private final EntityToolsForgeAdapter adapter;

    public EntityToolsRuntime() {
        this(new EntityToolsForgeAdapter());
    }

    EntityToolsRuntime(EntityToolsForgeAdapter adapter) {
        if (adapter == null) throw new IllegalArgumentException("Entity tools adapter is required.");
        this.adapter = adapter;
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        EntityPlayer player = event.entityPlayer;
        ItemStack held = player.getHeldItem();
        if (held == null || (held.getItem() != ModItems.copier && held.getItem() != ModItems.storageBox)) return;
        event.setCanceled(true);
        if (player.worldObj.isRemote) return;
        try {
            if (held.getItem() == ModItems.copier) captureCopier(player, held, event.target);
            else captureStorage(player, held, event.target);
        } catch (RuntimeException exception) {
            fail(player, exception);
        }
    }

    @SubscribeEvent
    public void onBlockInteract(PlayerInteractEvent event) {
        if (event.action != PlayerInteractEvent.Action.RIGHT_CLICK_BLOCK) return;
        EntityPlayer player = event.entityPlayer;
        ItemStack held = player.getHeldItem();
        if (held == null || (held.getItem() != ModItems.copier && held.getItem() != ModItems.storageBox)) return;
        if (player.worldObj.isRemote) return;
        event.setCanceled(true);
        int[] offset = faceOffset(event.face);
        try {
            if (held.getItem() == ModItems.copier)
                spawnCopier(player, held, event.x + offset[0] + 0.5D, event.y + offset[1], event.z + offset[2] + 0.5D);
            else spawnStorage(
                player,
                held,
                event.x + offset[0] + 0.5D,
                event.y + offset[1],
                event.z + offset[2] + 0.5D);
        } catch (RuntimeException exception) {
            fail(player, exception);
        }
    }

    private void captureCopier(EntityPlayer player, ItemStack stack, Entity target) {
        EntityCapture capture = adapter.capture(target);
        CopierState state = ItemCopier.loadState(stack);
        state.capture(capture);
        ItemCopier.saveState(stack, state);
        player.addChatMessage(
            new ChatComponentText(
                "已保存生物模板: " + capture.getEntityType()
                    + "（共 "
                    + state.getTemplates()
                        .size()
                    + " 个）"));
    }

    private void spawnCopier(EntityPlayer player, ItemStack stack, double x, double y, double z) {
        CopierState state = ItemCopier.loadState(stack);
        EntitySpawnSpec spec = state.copySelected(UUID.randomUUID());
        adapter.spawn(player.worldObj, x, y, z, spec);
        player.addChatMessage(new ChatComponentText("已生成模板副本；唯一 NPC ID 未复制。"));
    }

    private void captureStorage(EntityPlayer player, ItemStack stack, Entity target) {
        StorageBoxState state = ItemStorageBox.loadState(stack);
        if (state.isOccupied()) throw new IllegalStateException("收纳箱已占用；请先在方块上放出实体。");
        EntityCapture capture = adapter.capture(target);
        boolean creative = player.capabilities.isCreativeMode;
        String npcId = creative ? null
            : NpcIdentitySavedData.get()
                .getNpcId(target.getUniqueID());
        state.capture(capture, creative ? StorageMode.CREATIVE : StorageMode.SURVIVAL, npcId);
        ItemStorageBox.saveState(stack, state);
        if (!creative && !CustomNpcActorBinding.deleteForStorage(target)) target.setDead();
        player.addChatMessage(new ChatComponentText(creative ? "已保存创造模式实体模板；原实体保留。" : "实体已收纳；唯一 NPC ID 继续被占用。"));
    }

    private void spawnStorage(EntityPlayer player, ItemStack stack, double x, double y, double z) {
        StorageBoxState state = ItemStorageBox.loadState(stack);
        EntitySpawnSpec spec = state.previewRelease();
        adapter.spawn(player.worldObj, x, y, z, spec);
        state.consumeSuccessfulRelease();
        ItemStorageBox.saveState(stack, state);
        player.addChatMessage(new ChatComponentText(state.isOccupied() ? "已生成创造模式副本；模板仍保留。" : "实体已恢复；收纳箱已清空。"));
    }

    private static int[] faceOffset(int face) {
        switch (face) {
            case 0:
                return new int[] { 0, -1, 0 };
            case 1:
                return new int[] { 0, 1, 0 };
            case 2:
                return new int[] { 0, 0, -1 };
            case 3:
                return new int[] { 0, 0, 1 };
            case 4:
                return new int[] { -1, 0, 0 };
            case 5:
                return new int[] { 1, 0, 0 };
            default:
                throw new IllegalArgumentException("Invalid block face.");
        }
    }

    private static void fail(EntityPlayer player, RuntimeException exception) {
        String message = exception.getMessage();
        player.addChatMessage(
            new ChatComponentText(
                "§c实体工具操作失败: " + (message == null || message.trim()
                    .isEmpty() ? exception.getClass()
                        .getSimpleName() : message)));
    }
}
