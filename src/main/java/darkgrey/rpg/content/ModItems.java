package darkgrey.rpg.content;

import net.minecraft.creativetab.CreativeTabs;
import net.minecraft.item.Item;

import cpw.mods.fml.common.registry.GameRegistry;
import darkgrey.rpg.item.ItemBody;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.item.ItemEditorTool;
import darkgrey.rpg.item.ItemNominator;
import darkgrey.rpg.item.ItemStorageBox;

public final class ModItems {

    public static Item editorTool;
    public static Item copperCoin;
    public static Item nominator;
    public static Item copier;
    public static Item storageBox;
    public static Item body;

    private ModItems() {}

    public static void register() {
        editorTool = new ItemEditorTool().setUnlocalizedName("darkgrey_rpg.editor_tool")
            .setTextureName("darkgrey_rpg:editor")
            .setCreativeTab(CreativeTabs.tabTools)
            .setMaxStackSize(1);
        GameRegistry.registerItem(editorTool, "editor_tool");

        copperCoin = new Item().setUnlocalizedName("darkgrey_rpg.copper_coin")
            .setTextureName("minecraft:gold_nugget")
            .setCreativeTab(CreativeTabs.tabMisc)
            .setMaxStackSize(64);
        GameRegistry.registerItem(copperCoin, "copper_coin");

        nominator = new ItemNominator().setUnlocalizedName("darkgrey_rpg.nominator")
            .setTextureName("darkgrey_rpg:nominator")
            .setCreativeTab(CreativeTabs.tabTools)
            .setMaxStackSize(1);
        GameRegistry.registerItem(nominator, "nominator");

        copier = new ItemCopier().setUnlocalizedName("darkgrey_rpg.copier")
            .setTextureName("darkgrey_rpg:copier")
            .setCreativeTab(CreativeTabs.tabTools)
            .setMaxStackSize(1);
        GameRegistry.registerItem(copier, "copier");

        storageBox = new ItemStorageBox().setUnlocalizedName("darkgrey_rpg.storage_box")
            .setCreativeTab(CreativeTabs.tabTools)
            .setMaxStackSize(1)
            .setMaxDamage(0)
            .setHasSubtypes(true);
        GameRegistry.registerItem(storageBox, "storage_box");

        body = new ItemBody().setUnlocalizedName("darkgrey_rpg.body")
            .setTextureName("darkgrey_rpg:body")
            .setCreativeTab(CreativeTabs.tabMisc)
            .setMaxStackSize(1);
        GameRegistry.registerItem(body, "body");
    }
}
