package darkgrey.rpg.runtime;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectRepository;

public final class ActorBindingActions {

    private ActorBindingActions() {}

    public static boolean bind(ProjectRepository repository, EntityPlayer player, Entity target, String actorId) {
        ActorDefinition actor = repository.getSnapshot()
            .getActor(actorId);
        if (actor == null) {
            ChatMessages.error(player, "Unknown Actor ID: " + actorId);
            return false;
        }
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            ChatMessages.error(player, "The selected entity is not a CustomNPC+ NPC.");
            return false;
        }

        CustomNpcActorBinding.bind(target, actorId);
        ChatMessages
            .success(player, "Bound CNPC '" + CustomNpcActorBinding.getNpcName(target) + "' to Actor " + actorId + ".");
        return true;
    }

    public static boolean unbind(EntityPlayer player, Entity target) {
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            ChatMessages.error(player, "The selected entity is not a CustomNPC+ NPC.");
            return false;
        }

        String oldActorId = CustomNpcActorBinding.getActorId(target);
        if (oldActorId == null) {
            ChatMessages.info(player, "This CNPC has no DarkGrey RPG Actor binding.");
            return false;
        }

        CustomNpcActorBinding.unbind(target);
        ChatMessages.success(player, "Removed Actor binding " + oldActorId + ".");
        return true;
    }

    public static void inspect(ProjectRepository repository, EntityPlayer player, Entity target) {
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            ChatMessages.error(player, "The selected entity is not a CustomNPC+ NPC.");
            return;
        }

        String actorId = CustomNpcActorBinding.getActorId(target);
        ChatMessages.info(player, "CNPC: " + CustomNpcActorBinding.getNpcName(target));
        if (actorId == null) {
            ChatMessages.info(player, "Actor: <unbound>");
            return;
        }

        ActorDefinition actor = repository.getSnapshot()
            .getActor(actorId);
        if (actor == null) {
            ChatMessages.error(player, "Actor: " + actorId + " (missing from the loaded project)");
            return;
        }
        ChatMessages.info(player, "Actor: " + actor.getId() + " — " + actor.getDisplayName());
    }
}
