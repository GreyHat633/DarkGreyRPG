package darkgrey.rpg.gramophone;

import net.minecraft.block.BlockContainer;
import net.minecraft.block.material.Material;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.tileentity.TileEntity;
import net.minecraft.world.World;

import darkgrey.rpg.content.ModCreativeTabs;

public final class BlockGramophone extends BlockContainer {

    public BlockGramophone() {
        super(Material.wood);
        setBlockName("darkgrey_rpg.gramophone");
        setBlockTextureName("jukebox_side");
        setHardness(2);
        setResistance(6000000);
        setCreativeTab(ModCreativeTabs.DARKGREY_RPG);
    }

    @Override
    public TileEntity createNewTileEntity(World world, int meta) {
        return new TileGramophone();
    }

    @Override
    public int getMobilityFlag() {
        return 2;
    }

    @Override
    public void onBlockExploded(World world, int x, int y, int z, net.minecraft.world.Explosion explosion) {}

    @Override
    public boolean canEntityDestroy(net.minecraft.world.IBlockAccess world, int x, int y, int z,
        net.minecraft.entity.Entity entity) {
        return false;
    }

    @Override
    public void onNeighborBlockChange(World world, int x, int y, int z, net.minecraft.block.Block block) {
        if (world.isRemote) return;
        TileEntity tile = world.getTileEntity(x, y, z);
        if (tile instanceof TileGramophone) GramophoneServer.changed((TileGramophone) tile);
    }

    @Override
    public void breakBlock(World world, int x, int y, int z, net.minecraft.block.Block block, int meta) {
        TileEntity tile = world.getTileEntity(x, y, z);
        if (tile instanceof TileGramophone) GramophoneLocalServer.removed((TileGramophone) tile);
        super.breakBlock(world, x, y, z, block, meta);
    }

    @Override
    public boolean onBlockActivated(World world, int x, int y, int z, EntityPlayer player, int side, float hitX,
        float hitY, float hitZ) {
        if (!world.isRemote && player instanceof EntityPlayerMP && GramophoneServer.allowed(player)) {
            TileEntity tile = world.getTileEntity(x, y, z);
            if (tile instanceof TileGramophone) GramophoneNetwork.CHANNEL
                .sendTo(((TileGramophone) tile).snapshot(GramophonePacket.OPEN), (EntityPlayerMP) player);
        }
        return true;
    }
}
