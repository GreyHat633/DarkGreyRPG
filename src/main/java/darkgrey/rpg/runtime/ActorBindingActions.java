package darkgrey.rpg.runtime;

import java.util.Collections;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;

import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.nominator.NominatorEntityBinding;
import darkgrey.rpg.nominator.NominatorResult;
import darkgrey.rpg.nominator.NominatorSavedData;
import darkgrey.rpg.nominator.NominatorService;
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

        NominatorSavedData selections = NominatorSavedData.get();
        NominatorEntityBinding existing = selections.get(target.getUniqueID());
        NominatorResult result = NominatorService.bindEntity(
            true,
            target.getUniqueID(),
            NominatorService.entityType(target),
            target.dimension,
            actorId,
            existing == null ? Collections.<String>emptyList() : existing.getGroupIds(),
            existing == null ? null : existing.getStoryId(),
            repository.getSnapshot(),
            NpcIdentitySavedData.get(),
            selections);
        if (!result.isAccepted()) {
            ChatMessages.error(player, "Actor binding rejected: " + result.getCode());
            return false;
        }
        ChatMessages
            .success(player, "Bound CNPC '" + CustomNpcActorBinding.getNpcName(target) + "' to Actor " + actorId + ".");
        return true;
    }

    public static boolean unbind(EntityPlayer player, Entity target) {
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            ChatMessages.error(player, "The selected entity is not a CustomNPC+ NPC.");
            return false;
        }

        String oldActorId = NpcIdentitySavedData.get()
            .getNpcId(target.getUniqueID());
        if (oldActorId == null) {
            ChatMessages.info(player, "This CNPC has no DarkGrey RPG Actor binding.");
            return false;
        }

        NominatorSavedData selections = NominatorSavedData.get();
        NominatorEntityBinding existing = selections.get(target.getUniqueID());
        NpcIdentitySavedData.get()
            .unbindHost(target.getUniqueID());
        if (existing != null) selections
            .put(new NominatorEntityBinding(target.getUniqueID(), null, existing.getGroupIds(), existing.getStoryId()));
        ChatMessages.success(player, "Removed Actor binding " + oldActorId + ".");
        return true;
    }

    public static void inspect(ProjectRepository repository, EntityPlayer player, Entity target) {
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            ChatMessages.error(player, "The selected entity is not a CustomNPC+ NPC.");
            return;
        }

        String actorId = NpcIdentitySavedData.get()
            .getNpcId(target.getUniqueID());
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
