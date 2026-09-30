package darkgrey.rpg.gramophone;

import net.minecraft.block.Block;
import net.minecraft.client.renderer.RenderBlocks;
import net.minecraft.client.renderer.Tessellator;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.util.IIcon;
import net.minecraft.world.IBlockAccess;
import net.minecraftforge.client.IItemRenderer;
import net.minecraftforge.client.MinecraftForgeClient;

import org.lwjgl.opengl.GL11;

import cpw.mods.fml.client.registry.ISimpleBlockRenderingHandler;
import cpw.mods.fml.client.registry.RenderingRegistry;
import cpw.mods.fml.common.registry.GameRegistry;

/** Original shared world/item geometry: walnut plinth, record, arm and brass bell. */
public final class GramophoneRenderer implements ISimpleBlockRenderingHandler {

    private static final double[][] PARTS = { { 1, 0, 1, 15, 2, 15, 0x523423 }, { 2, 2, 2, 14, 4, 14, 0x805332 },
        { 3, 4, 3, 11, 4.5, 11, 0x181B20 }, { 4, 4.5, 2, 10, 5, 12, 0x24282D }, { 2, 4.5, 4, 12, 5, 10, 0x24282D },
        { 6, 5, 6, 8, 5.15, 8, 0xB04B38 }, { 6.7, 5.15, 6.7, 7.3, 5.8, 7.3, 0xD1B374 },
        { 12, 4, 3, 13, 7, 4, 0xD5B35D }, { 9, 6.5, 3, 13, 7, 4, 0xD5B35D }, { 9, 5.5, 3, 10, 7, 4, 0xC2994B },
        { 11, 4, 10, 13, 10, 12, 0xA97C32 }, { 9, 9, 10, 13, 11, 12, 0xCEA04B }, { 7, 9, 9, 10, 12, 13, 0xC99B43 },
        { 5, 8, 8, 7, 9, 14, 0xDAB95E }, { 5, 12, 8, 7, 13, 14, 0xDAB95E }, { 5, 9, 8, 7, 12, 9, 0xDAB95E },
        { 5, 9, 13, 7, 12, 14, 0xDAB95E }, { 3, 7, 7, 5, 8, 15, 0xE2C574 }, { 3, 13, 7, 5, 14, 15, 0xE2C574 },
        { 3, 8, 7, 5, 13, 8, 0xE2C574 }, { 3, 8, 14, 5, 13, 15, 0xE2C574 }, { 6.9, 9, 9, 7.05, 12, 13, 0x6B4B24 } };

    public static void register() {
        BlockGramophone.clientRenderId = RenderingRegistry.getNextAvailableRenderId();
        GramophoneRenderer renderer = new GramophoneRenderer();
        cpw.mods.fml.client.registry.ClientRegistry.bindTileEntitySpecialRenderer(
            TileGramophone.class,
            new net.minecraft.client.renderer.tileentity.TileEntitySpecialRenderer() {

                @Override
                public void renderTileEntityAt(net.minecraft.tileentity.TileEntity tile, double x, double y, double z,
                    float partialTicks) {
                    GramophoneClient.renderRange((TileGramophone) tile, x, y, z);
                }
            });
        RenderingRegistry.registerBlockHandler(renderer);
        Block block = GameRegistry.findBlock("darkgrey_rpg", "gramophone");
        MinecraftForgeClient.registerItemRenderer(Item.getItemFromBlock(block), new IItemRenderer() {

            @Override
            public boolean handleRenderType(ItemStack item, ItemRenderType type) {
                return type == ItemRenderType.EQUIPPED_FIRST_PERSON;
            }

            @Override
            public boolean shouldUseRenderHelper(ItemRenderType type, ItemStack item, ItemRendererHelper helper) {
                return helper == ItemRendererHelper.EQUIPPED_BLOCK;
            }

            @Override
            public void renderItem(ItemRenderType type, ItemStack item, Object... data) {
                GL11.glPushMatrix();
                try {
                    // Cancel Forge's block-origin shift before positioning our centered model.
                    // Match the vanilla block size and lower-right first-person anchor.
                    GL11.glTranslatef(0.5F, 1.0F, 0.5F);

                    renderer.renderInventoryBlock(
                        block,
                        item.getItemDamage(),
                        renderer.getRenderId(),
                        (RenderBlocks) data[0]);
                } finally {
                    GL11.glPopMatrix();
                }
            }
        });
    }

    @Override
    public boolean renderWorldBlock(IBlockAccess world, int x, int y, int z, Block block, int id,
        RenderBlocks renderer) {
        boolean all = renderer.renderAllFaces;
        try {
            renderer.renderAllFaces = true;
            for (double[] part : PARTS) {
                bounds(renderer, part);
                int color = (int) part[6];
                renderer.renderStandardBlockWithColorMultiplier(
                    block,
                    x,
                    y,
                    z,
                    (color >> 16 & 255) / 255F,
                    (color >> 8 & 255) / 255F,
                    (color & 255) / 255F);
            }
        } finally {
            renderer.renderAllFaces = all;
            renderer.setRenderBoundsFromBlock(block);
        }
        return true;
    }

    @Override
    public void renderInventoryBlock(Block block, int metadata, int id, RenderBlocks renderer) {
        GL11.glPushMatrix();
        GL11.glPushAttrib(GL11.GL_CURRENT_BIT);
        try {
            GL11.glTranslatef(-0.5F, -0.5F, -0.5F);
            IIcon icon = block.getIcon(0, metadata);
            Tessellator tess = Tessellator.instance;
            for (double[] part : PARTS) {
                bounds(renderer, part);
                int color = (int) part[6];
                GL11.glColor4f((color >> 16 & 255) / 255F, (color >> 8 & 255) / 255F, (color & 255) / 255F, 1);
                for (int face = 0; face < 6; face++) {
                    tess.startDrawingQuads();
                    switch (face) {
                        case 0:
                            tess.setNormal(0, -1, 0);
                            renderer.renderFaceYNeg(block, 0, 0, 0, icon);
                            break;
                        case 1:
                            tess.setNormal(0, 1, 0);
                            renderer.renderFaceYPos(block, 0, 0, 0, icon);
                            break;
                        case 2:
                            tess.setNormal(0, 0, -1);
                            renderer.renderFaceZNeg(block, 0, 0, 0, icon);
                            break;
                        case 3:
                            tess.setNormal(0, 0, 1);
                            renderer.renderFaceZPos(block, 0, 0, 0, icon);
                            break;
                        case 4:
                            tess.setNormal(-1, 0, 0);
                            renderer.renderFaceXNeg(block, 0, 0, 0, icon);
                            break;
                        default:
                            tess.setNormal(1, 0, 0);
                            renderer.renderFaceXPos(block, 0, 0, 0, icon);
                            break;
                    }
                    tess.draw();
                }
            }
        } finally {
            renderer.setRenderBoundsFromBlock(block);
            GL11.glPopAttrib();
            GL11.glPopMatrix();
        }
    }

    private static void bounds(RenderBlocks renderer, double[] part) {
        renderer.setRenderBounds(part[0] / 16, part[1] / 16, part[2] / 16, part[3] / 16, part[4] / 16, part[5] / 16);
    }

    @Override
    public boolean shouldRender3DInInventory(int id) {
        return true;
    }

    @Override
    public int getRenderId() {
        return BlockGramophone.clientRenderId;
    }
}
