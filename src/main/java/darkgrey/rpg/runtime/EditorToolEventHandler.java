package darkgrey.rpg.runtime;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;
import net.minecraftforge.event.entity.player.PlayerInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.live.LivePickService;
import darkgrey.rpg.project.ProjectRepository;

public final class EditorToolEventHandler {

    private final ProjectRepository repository;
    private final EditorSessionManager sessions;
    private final LivePickService livePicks;

    public EditorToolEventHandler(ProjectRepository repository, EditorSessionManager sessions) {
        this(repository, sessions, null);
    }

    public EditorToolEventHandler(ProjectRepository repository, EditorSessionManager sessions,
        LivePickService livePicks) {
        this.repository = repository;
        this.sessions = sessions;
        this.livePicks = livePicks;
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        EntityPlayer player = event.entityPlayer;
        ItemStack heldItem = player.getHeldItem();
        if (heldItem == null || heldItem.getItem() != ModItems.editorTool) {
            return;
        }

        Entity target = event.target;
        if (player.worldObj.isRemote) {
            event.setCanceled(true);
            return;
        }
        if (livePicks != null && livePicks.handleEntity((net.minecraft.entity.player.EntityPlayerMP) player, target)) {
            event.setCanceled(true);
            return;
        }

        if (player.isSneaking()) {
            ActorBindingActions.inspect(repository, player, target);
        } else {
            String selectedActor = sessions.getSelectedActor(player);
            if (selectedActor == null) {
                ActorBindingActions.inspect(repository, player, target);
                ChatMessages.info(player, "Use /dgrpg actor select <id> before binding.");
            } else {
                ActorBindingActions.bind(repository, player, target, selectedActor);
            }
        }
        event.setCanceled(true);
    }

    @SubscribeEvent
    public void onBlockInteract(PlayerInteractEvent event) {
        if (event.action != PlayerInteractEvent.Action.RIGHT_CLICK_BLOCK || event.entityPlayer.worldObj.isRemote
            || livePicks == null) {
            return;
        }
        ItemStack heldItem = event.entityPlayer.getHeldItem();
        if (heldItem == null || heldItem.getItem() != ModItems.editorTool) {
            return;
        }
        if (livePicks
            .handleBlock((net.minecraft.entity.player.EntityPlayerMP) event.entityPlayer, event.x, event.y, event.z)) {
            event.setCanceled(true);
        }
    }
}
