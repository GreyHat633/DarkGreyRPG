package darkgrey.rpg.content;

import net.minecraft.item.Item;

import cpw.mods.fml.common.registry.GameRegistry;
import darkgrey.rpg.item.ItemBody;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.item.ItemEditorTool;
import darkgrey.rpg.item.ItemNominator;
import darkgrey.rpg.item.ItemStorageBox;

public final class ModItems {

    public static Item editorTool;
    public static Item nominator;
    public static Item copier;
    public static Item storageBox;
    public static Item body;
    public static Item inspectorGoggles;

    private ModItems() {}

    public static void register() {
        editorTool = new ItemEditorTool().setUnlocalizedName("darkgrey_rpg.editor_tool")
            .setTextureName("darkgrey_rpg:editor")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG)
            .setMaxStackSize(1);
        GameRegistry.registerItem(editorTool, "editor_tool");

        nominator = new ItemNominator().setUnlocalizedName("darkgrey_rpg.nominator")
            .setTextureName("darkgrey_rpg:nominator")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG)
            .setMaxStackSize(1);
        GameRegistry.registerItem(nominator, "nominator");

        copier = new ItemCopier().setUnlocalizedName("darkgrey_rpg.copier")
            .setTextureName("darkgrey_rpg:copier")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG)
            .setMaxStackSize(1);
        GameRegistry.registerItem(copier, "copier");

        storageBox = new ItemStorageBox().setUnlocalizedName("darkgrey_rpg.storage_box")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG)
            .setMaxStackSize(1)
            .setMaxDamage(0)
            .setHasSubtypes(true);
        GameRegistry.registerItem(storageBox, "storage_box");

        body = new ItemBody().setUnlocalizedName("darkgrey_rpg.body")
            .setTextureName("darkgrey_rpg:body")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG)
            .setMaxStackSize(1);
        GameRegistry.registerItem(body, "body");
        inspectorGoggles = new darkgrey.rpg.creator.ItemInspectorGoggles()
            .setUnlocalizedName("darkgrey_rpg.inspector_goggles")
            .setTextureName("darkgrey_rpg:inspector_goggles")
            .setCreativeTab(ModCreativeTabs.DARKGREY_RPG);
        GameRegistry.registerItem(inspectorGoggles, "inspector_goggles");
    }
}
