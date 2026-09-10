package darkgrey.rpg.story.canonical.server;

import java.util.Collections;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;

/** Server-generated immutable capability description; never accepted from client data. */
public final class CanonicalActorCandidate {

    private final UUID player;
    private final ProjectSnapshot project;
    private final String storyId;
    private final String displayName;
    private final String actorId;
    private final String portId;
    private final String status;
    private final NBTTagCompound previousState;

    CanonicalActorCandidate(UUID player, ProjectSnapshot project, String storyId, String actorId, String portId,
        String status, CanonicalStoryInstanceSnapshot previous) {
        this.player = player;
        this.project = project;
        this.storyId = storyId;
        this.displayName = project.getCanonicalStory(storyId)
            .getDisplayName();
        this.actorId = actorId;
        this.portId = portId;
        this.status = status;
        this.previousState = state(previous);
    }

    public String getStoryId() {
        return storyId;
    }

    public String getDisplayName() {
        return displayName;
    }

    public String getActorId() {
        return actorId;
    }

    public String getPortId() {
        return portId;
    }

    public String getStatus() {
        return status;
    }

    boolean matches(UUID currentPlayer, ProjectSnapshot currentProject, CanonicalActorCandidate current) {
        return player.equals(currentPlayer) && project == currentProject
            && current != null
            && storyId.equals(current.storyId)
            && actorId.equals(current.actorId)
            && status.equals(current.status)
            && portId.equals(current.portId)
            && previousState.equals(current.previousState);
    }

    private static NBTTagCompound state(CanonicalStoryInstanceSnapshot snapshot) {
        return CanonicalStoryInstanceNbtCodec.encode(
            snapshot == null ? Collections.<CanonicalStoryInstanceSnapshot>emptyList()
                : Collections.singletonList(snapshot));
    }
}
