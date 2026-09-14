package darkgrey.rpg.task.forge;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/** Short-lived server capability for choosing one actor-bound Task submit target. */
final class CanonicalTaskSubmitChoiceStore {

    private final Map<UUID, Choice> pending = new HashMap<UUID, Choice>();
    private long nextToken = 1;

    synchronized Choice offer(UUID player, UUID entity, int entityId, int dimension, List<Candidate> candidates,
        long time) {
        if (player == null || entity == null || candidates == null || candidates.size() < 2 || candidates.size() > 256)
            throw new IllegalArgumentException("A bounded Task submit choice is required.");
        if (nextToken == Long.MAX_VALUE) {
            pending.clear();
            nextToken = 1;
        }
        Choice choice = new Choice(nextToken++, entity, entityId, dimension, candidates, time + 60000L);
        pending.put(player, choice);
        return choice;
    }

    synchronized Choice consume(UUID player, long token, long time) {
        Choice choice = pending.get(player);
        if (choice == null || choice.token != token) return null;
        pending.remove(player);
        return time > choice.expiresAt ? null : choice;
    }

    synchronized void forget(UUID player) {
        pending.remove(player);
    }

    static final class Candidate {

        private final String storyId;
        private final String placementId;
        private final String objectiveId;
        private final String actorId;
        private final long activationTime;
        private final String displayName;

        Candidate(String storyId, String placementId, String objectiveId, String actorId, long activationTime,
            String displayName) {
            this.storyId = storyId;
            this.placementId = placementId;
            this.objectiveId = objectiveId;
            this.actorId = actorId;
            this.activationTime = activationTime;
            this.displayName = displayName;
        }

        String getStoryId() {
            return storyId;
        }

        String getPlacementId() {
            return placementId;
        }

        String getObjectiveId() {
            return objectiveId;
        }

        String getActorId() {
            return actorId;
        }

        long getActivationTime() {
            return activationTime;
        }

        String getDisplayName() {
            return displayName;
        }
    }

    static final class Choice {

        private final long token;
        private final UUID entity;
        private final int entityId;
        private final int dimension;
        private final List<Candidate> candidates;
        private final long expiresAt;

        private Choice(long token, UUID entity, int entityId, int dimension, List<Candidate> candidates,
            long expiresAt) {
            this.token = token;
            this.entity = entity;
            this.entityId = entityId;
            this.dimension = dimension;
            this.candidates = Collections.unmodifiableList(new ArrayList<Candidate>(candidates));
            this.expiresAt = expiresAt;
        }

        long getToken() {
            return token;
        }

        UUID getEntity() {
            return entity;
        }

        int getEntityId() {
            return entityId;
        }

        int getDimension() {
            return dimension;
        }

        List<Candidate> getCandidates() {
            return candidates;
        }
    }
}
