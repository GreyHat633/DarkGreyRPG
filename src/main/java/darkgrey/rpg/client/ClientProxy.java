package darkgrey.rpg.client;

import java.util.List;
import java.util.UUID;

import net.minecraft.client.Minecraft;
import net.minecraft.entity.Entity;
import net.minecraftforge.common.MinecraftForge;

import cpw.mods.fml.common.FMLCommonHandler;
import darkgrey.rpg.client.gui.GuiCopierTemplates;
import darkgrey.rpg.client.gui.GuiNominatorEntity;
import darkgrey.rpg.client.gui.GuiNominatorInventory;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;
import darkgrey.rpg.proxy.CommonProxy;

public final class ClientProxy extends CommonProxy {

    @Override
    public boolean isCurrentClientConnection(Object connection) {
        return connection != null && Minecraft.getMinecraft()
            .getNetHandler() == connection;
    }

    @Override
    public void acceptCreatorSnapshot(int kind, net.minecraft.nbt.NBTTagCompound data) {
        if (kind == 0) CreatorInspectClient.accept(data);
        else CanonicalTaskClientStore.accept(data);
    }

    private NominatorCatalog pendingInventoryCatalog;
    private long pendingInventoryRevision = -1L;
    private long pendingInventoryCatalogRevision = -1L;

    @Override
    public void registerClientDialogueNetwork() {
        // All discriminators are registered on both physical sides by DialogueNetwork.
        FMLCommonHandler.instance()
            .bus()
            .register(new ClientQuestKeyHandler());
        MinecraftForge.EVENT_BUS.register(new NominatorClientRuntime());
        darkgrey.rpg.client.session.CanonicalPresentationBridge bridge = new darkgrey.rpg.client.session.CanonicalPresentationBridge();
        MinecraftForge.EVENT_BUS.register(bridge);
        FMLCommonHandler.instance()
            .bus()
            .register(bridge);
        CreatorInspectClient inspect = new CreatorInspectClient();
        MinecraftForge.EVENT_BUS.register(inspect);
        FMLCommonHandler.instance()
            .bus()
            .register(inspect);
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
        long catalogRevision, darkgrey.rpg.nominator.NominatorCatalog catalog) {
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
                    catalogRevision,
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
    public void openNominatorInventoryGui(NominatorCatalog catalog, long revision, long catalogRevision,
        int selectedSlot) {
        if (Minecraft.getMinecraft().currentScreen instanceof GuiNominatorInventory) {
            ((GuiNominatorInventory) Minecraft.getMinecraft().currentScreen)
                .applyServerSnapshot(catalog, revision, catalogRevision);
            return;
        }
        pendingInventoryCatalog = catalog;
        pendingInventoryRevision = revision;
        pendingInventoryCatalogRevision = catalogRevision;
    }

    @Override
    public Object createNominatorInventoryGui(ContainerNominatorInventory container) {
        GuiNominatorInventory gui = new GuiNominatorInventory(container);
        if (pendingInventoryCatalog != null) {
            gui.applyServerSnapshot(pendingInventoryCatalog, pendingInventoryRevision, pendingInventoryCatalogRevision);
            pendingInventoryCatalog = null;
            pendingInventoryRevision = -1L;
            pendingInventoryCatalogRevision = -1L;
        }
        return gui;
    }

    @Override
    public void openCopierGui(net.minecraft.item.ItemStack stack) {
        if (stack != null) Minecraft.getMinecraft()
            .displayGuiScreen(new GuiCopierTemplates(stack));
    }
}
