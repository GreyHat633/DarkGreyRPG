package darkgrey.rpg.story.runtime;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.quest.runtime.QuestStatus;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.story.StoryDefinition;

public final class StoryExecutionContext {

    private final EntityPlayerMP player;
    private final StoryDefinition story;
    private final StoryInstance instance;
    private final DialogueSessionManager dialogues;
    private final QuestRuntimeService quests;

    public StoryExecutionContext(EntityPlayerMP player, StoryDefinition story, StoryInstance instance,
        DialogueSessionManager dialogues, QuestRuntimeService quests) {
        this.player = player;
        this.story = story;
        this.instance = instance;
        this.dialogues = dialogues;
        this.quests = quests;
    }

    public EntityPlayerMP getPlayer() {
        return player;
    }

    public StoryInstance getInstance() {
        return instance;
    }

    public StoryDefinition getStory() {
        return story;
    }

    public boolean hasConnection(String nodeId, String output) {
        return story.findConnection(nodeId, output) != null;
    }

    public boolean startDialogue(String dialogueId) {
        return dialogues.start(player, dialogueId);
    }

    public boolean startQuest(String questId) {
        return quests.start(player, questId);
    }

    public boolean completeQuest(String questId) {
        return quests.complete(player, questId);
    }

    public QuestStatus getQuestStatus(String questId) {
        return quests.getStatus(player, questId);
    }

    public String getVariable(String variable) {
        return PlayerStoryData.get(player)
            .getVariable(player, story.getId(), variable);
    }

    public void setVariable(String variable, String value) {
        PlayerStoryData.get(player)
            .setVariable(player, story.getId(), variable, value);
    }

    public boolean compareVariable(String variable, String operator, String expected) {
        return compare(getVariable(variable), operator, expected);
    }

    public boolean hasItem(String itemId, int metadata, int amount) {
        int found = 0;
        for (ItemStack stack : player.inventory.mainInventory) {
            if (stack != null && itemMatches(itemId, stack.getItem())
                && (metadata < 0 || metadata == stack.getItemDamage())) {
                found += stack.stackSize;
                if (found >= amount) {
                    return true;
                }
            }
        }
        return false;
    }

    public boolean giveItem(String itemId, int metadata, int amount) {
        Object value = Item.itemRegistry.getObject(itemId);
        if (!(value instanceof Item)) {
            return false;
        }
        ItemStack stack = new ItemStack((Item) value, amount, Math.max(0, metadata));
        if (!player.inventory.addItemStackToInventory(stack) && stack.stackSize > 0) {
            player.dropPlayerItemWithRandomChoice(stack, false);
        }
        player.inventoryContainer.detectAndSendChanges();
        return true;
    }

    public void giveExperience(int amount) {
        player.addExperience(amount);
    }

    public void sendMessage(String message) {
        ChatMessages.info(player, message);
    }

    private static boolean itemMatches(String configured, Item item) {
        Object name = Item.itemRegistry.getNameForObject(item);
        return name != null && configured.equalsIgnoreCase(String.valueOf(name));
    }

    private static boolean compare(String actual, String operator, String expected) {
        if ("equals".equals(operator)) {
            return actual.equals(expected);
        }
        if ("not_equals".equals(operator)) {
            return !actual.equals(expected);
        }
        try {
            double left = Double.parseDouble(actual.isEmpty() ? "0" : actual);
            double right = Double.parseDouble(expected);
            if ("greater".equals(operator)) {
                return left > right;
            }
            if ("greater_or_equal".equals(operator)) {
                return left >= right;
            }
            if ("less".equals(operator)) {
                return left < right;
            }
            if ("less_or_equal".equals(operator)) {
                return left <= right;
            }
        } catch (NumberFormatException ignored) {
            return false;
        }
        return false;
    }
}
