package darkgrey.rpg.story.canonical.forge;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.player.PlayerRpgSavedData;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatch;

/** Executes the deliberately bounded Stage 5 Story reward/action subset on the server player. */
public final class CanonicalStoryForgeActionExecutor implements CanonicalStoryForgeManager.ActionExecutor {

    @Override
    public boolean execute(EntityPlayerMP player, CanonicalStoryDispatch action) {
        if (player == null || action == null)
            throw new IllegalArgumentException("Server player and canonical Story action are required.");
        CanonicalStoryActionConfiguration configuration = CanonicalStoryActionConfiguration
            .parse(action.getActionProperties());
        String storyId = action.getSnapshot()
            .getStoryId();
        String receipt = action.getSnapshot()
            .getActivationTime() + ":"
            + action.getPlacementId();
        PlayerRpgSavedData progress = PlayerRpgSavedData.get();
        if (!progress.claimReward(player.getUniqueID(), storyId, receipt)) return true;
        try {
            boolean applied = executeOnce(player, configuration);
            if (!applied) progress.releaseRewardClaim(player.getUniqueID(), storyId, receipt);
            return applied;
        } catch (RuntimeException failure) {
            progress.releaseRewardClaim(player.getUniqueID(), storyId, receipt);
            throw failure;
        }
    }

    private static boolean executeOnce(EntityPlayerMP player, CanonicalStoryActionConfiguration configuration) {
        if (CanonicalStoryActionConfiguration.GIVE_ITEM.equals(configuration.getType()))
            return giveItem(player, configuration);
        if (CanonicalStoryActionConfiguration.GIVE_XP.equals(configuration.getType())) {
            darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions.applyXpDelta(player, configuration.getAmount());
            return true;
        }
        if (CanonicalStoryActionConfiguration.SEND_MESSAGE.equals(configuration.getType())) {
            ChatMessages.info(player, configuration.getMessage());
            return true;
        }
        return CanonicalStoryInstantActions.apply(player, configuration);
    }

    private static boolean giveItem(EntityPlayerMP player, CanonicalStoryActionConfiguration configuration) {
        if (configuration.getAmount() == 0) return true;
        if (configuration.isLegacyRegistryItem()) return giveLegacyItem(player, configuration);
        ItemStackDefinition definition = ItemIdentitySavedData.get()
            .getItem(configuration.getItemId());
        if (definition == null) return false;
        return applyItem(player, definition.createStack(1), configuration.getAmount());
    }

    private static boolean applyItem(EntityPlayerMP player, ItemStack prototype, int amount) {
        ItemStack[] staged = darkgrey.rpg.task.forge.CanonicalTaskInventory.copy(player.inventory.mainInventory);
        if (!darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions.applyItem(staged, prototype, amount)) return false;
        System.arraycopy(staged, 0, player.inventory.mainInventory, 0, staged.length);
        player.inventory.markDirty();
        player.inventoryContainer.detectAndSendChanges();
        return true;
    }

    private static boolean giveLegacyItem(EntityPlayerMP player, CanonicalStoryActionConfiguration configuration) {
        Object registered = Item.itemRegistry.getObject(configuration.getItemId());
        if (!(registered instanceof Item)) return false;
        return applyItem(
            player,
            new ItemStack((Item) registered, 1, configuration.getMetadata()),
            configuration.getAmount());
    }
}
