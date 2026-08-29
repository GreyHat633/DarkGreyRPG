package darkgrey.rpg.content;

import net.minecraft.creativetab.CreativeTabs;
import net.minecraft.item.Item;

import cpw.mods.fml.common.registry.GameRegistry;
import darkgrey.rpg.item.ItemEditorTool;

public final class ModItems {

    public static Item editorTool;
    public static Item copperCoin;

    private ModItems() {}

    public static void register() {
        editorTool = new ItemEditorTool().setUnlocalizedName("darkgrey_rpg.editor_tool")
            .setTextureName("minecraft:blaze_rod")
            .setCreativeTab(CreativeTabs.tabTools)
            .setMaxStackSize(1);
        GameRegistry.registerItem(editorTool, "editor_tool");

        copperCoin = new Item().setUnlocalizedName("darkgrey_rpg.copper_coin")
            .setTextureName("minecraft:gold_nugget")
            .setCreativeTab(CreativeTabs.tabMisc)
            .setMaxStackSize(64);
        GameRegistry.registerItem(copperCoin, "copper_coin");
    }
}
