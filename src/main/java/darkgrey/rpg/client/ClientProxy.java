package darkgrey.rpg.client;

import java.util.List;
import java.util.UUID;

import net.minecraft.client.Minecraft;
import net.minecraft.entity.Entity;

import cpw.mods.fml.common.FMLCommonHandler;
import darkgrey.rpg.client.gui.GuiCopierTemplates;
import darkgrey.rpg.client.gui.GuiNominatorEntity;
import darkgrey.rpg.client.gui.GuiNominatorInventory;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.proxy.CommonProxy;

public final class ClientProxy extends CommonProxy {

    @Override
    public void registerClientDialogueNetwork() {
        // All discriminators are registered on both physical sides by DialogueNetwork.
        FMLCommonHandler.instance()
            .bus()
            .register(new ClientQuestKeyHandler());
    }

    @Override
    public void openNominatorEntityGui(Entity entity) {
        if (entity != null) Minecraft.getMinecraft()
            .displayGuiScreen(new GuiNominatorEntity(entity.getEntityId(), entity.getUniqueID()));
    }

    @Override
    public void openNominatorEntityGui(int entityId, UUID entityUuid, String individual, List<String> groups,
        String story, long revision) {
        Minecraft.getMinecraft()
            .displayGuiScreen(new GuiNominatorEntity(entityId, entityUuid, individual, groups, story, revision));
    }

    @Override
    public void openNominatorEntityGui(int entityId, UUID entityUuid, String displayName, String entityType,
        String individual, List<String> groups, List<String> typeGroups, String story, long revision,
        darkgrey.rpg.nominator.NominatorCatalog catalog) {
        Minecraft.getMinecraft()
            .displayGuiScreen(
                new GuiNominatorEntity(
                    entityId,
                    entityUuid,
                    displayName,
                    entityType,
                    individual,
                    groups,
                    typeGroups,
                    story,
                    revision,
                    catalog));
    }

    @Override
    public void openNominatorInventoryGui() {
        Minecraft.getMinecraft()
            .displayGuiScreen(new GuiNominatorInventory());
    }

    @Override
    public void openNominatorInventoryGui(NominatorCatalog catalog, long revision, int selectedSlot) {
        Minecraft.getMinecraft()
            .displayGuiScreen(new GuiNominatorInventory(catalog, revision, selectedSlot));
    }

    @Override
    public void openCopierGui(net.minecraft.item.ItemStack stack) {
        if (stack != null) Minecraft.getMinecraft()
            .displayGuiScreen(new GuiCopierTemplates(stack));
    }
}
