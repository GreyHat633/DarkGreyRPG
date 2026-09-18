package darkgrey.rpg.proxy;

import java.util.List;
import java.util.UUID;

import net.minecraft.entity.Entity;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;

public class CommonProxy {

    public void acceptGramophone(darkgrey.rpg.gramophone.GramophonePacket packet) {}

    public void acceptGramophoneMedia(darkgrey.rpg.gramophone.GramophoneMediaPacket packet) {}

    public boolean isCurrentClientConnection(Object connection) {
        return false;
    }

    public void acceptNominatorResult(net.minecraft.nbt.NBTTagCompound data, NominatorCatalog catalog) {}

    public void registerClientDialogueNetwork() {}

    public void acceptCreatorSnapshot(int kind, net.minecraft.nbt.NBTTagCompound data) {}

    public void openNominatorEntityGui(Entity entity) {}

    public void openNominatorEntityGui(int entityId, UUID entityUuid, String individual, List<String> groups,
        String story, long revision) {}

    public void openNominatorEntityGui(int entityId, UUID entityUuid, String displayName, String entityType,
        String individual, List<String> groups, List<String> typeGroups, String story, long revision,
        long catalogRevision, NominatorCatalog catalog) {}

    public void openNominatorInventoryGui() {}

    public void openNominatorInventoryGui(NominatorCatalog catalog, long revision, int selectedSlot) {}

    public void openNominatorInventoryGui(NominatorCatalog catalog, long revision, long catalogRevision,
        int selectedSlot) {}

    public Object createNominatorInventoryGui(ContainerNominatorInventory container) {
        return null;
    }

    public void openCopierGui(ItemStack stack) {}
}
