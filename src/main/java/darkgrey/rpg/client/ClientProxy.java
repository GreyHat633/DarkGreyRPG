package darkgrey.rpg.client;

import cpw.mods.fml.common.FMLCommonHandler;
import darkgrey.rpg.proxy.CommonProxy;

public final class ClientProxy extends CommonProxy {

    @Override
    public void registerClientDialogueNetwork() {
        // All discriminators are registered on both physical sides by DialogueNetwork.
        FMLCommonHandler.instance()
            .bus()
            .register(new ClientQuestKeyHandler());
    }
}
