package darkgrey.rpg.story.canonical.forge;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

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
        if (CanonicalStoryActionConfiguration.GIVE_ITEM.equals(configuration.getType()))
            return giveItem(player, configuration);
        if (CanonicalStoryActionConfiguration.GIVE_XP.equals(configuration.getType())) {
            player.addExperience(configuration.getAmount());
            return true;
        }
        if (CanonicalStoryActionConfiguration.SEND_MESSAGE.equals(configuration.getType())) {
            ChatMessages.info(player, configuration.getMessage());
            return true;
        }
        return false;
    }

    private static boolean giveItem(EntityPlayerMP player, CanonicalStoryActionConfiguration configuration) {
        Object registered = Item.itemRegistry.getObject(configuration.getItemId());
        if (!(registered instanceof Item)) return false;
        ItemStack stack = new ItemStack((Item) registered, configuration.getAmount(), configuration.getMetadata());
        if (!player.inventory.addItemStackToInventory(stack) && stack.stackSize > 0)
            player.dropPlayerItemWithRandomChoice(stack, false);
        player.inventoryContainer.detectAndSendChanges();
        return true;
    }
}
