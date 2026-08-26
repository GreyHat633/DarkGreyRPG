package darkgrey.rpg.runtime;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import net.minecraft.entity.player.EntityPlayer;

public final class EditorSessionManager {

    private final Map<UUID, String> selectedActors = new ConcurrentHashMap<UUID, String>();

    public void selectActor(EntityPlayer player, String actorId) {
        selectedActors.put(player.getUniqueID(), actorId);
    }

    public String getSelectedActor(EntityPlayer player) {
        return selectedActors.get(player.getUniqueID());
    }

    public void clear(EntityPlayer player) {
        selectedActors.remove(player.getUniqueID());
    }
}
