package darkgrey.rpg.content;

import net.minecraft.creativetab.CreativeTabs;
import net.minecraft.item.Item;

/** Creative inventory grouping for all DarkGrey RPG content. */
public final class ModCreativeTabs {

    public static final CreativeTabs DARKGREY_RPG = new CreativeTabs("darkgrey_rpg") {

        @Override
        public Item getTabIconItem() {
            return ModItems.editorTool;
        }
    };

    private ModCreativeTabs() {}
}
