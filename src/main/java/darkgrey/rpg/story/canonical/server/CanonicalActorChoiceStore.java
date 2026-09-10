package darkgrey.rpg.story.canonical.server;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/** Short-lived server-only choice capabilities, bound to player and physical actor entity. */
public final class CanonicalActorChoiceStore {

    private final Map<UUID, Choice> pending = new HashMap<UUID, Choice>();
    private long nextToken = 1;

    public synchronized Choice offer(UUID player, UUID entity, int entityId, int dimension,
        List<CanonicalActorCandidate> candidates, long time) {
        if (player == null || entity == null || candidates == null || candidates.size() < 2 || candidates.size() > 256)
            throw new IllegalArgumentException("A bounded multiple-Story choice is required.");
        if (nextToken == Long.MAX_VALUE) {
            pending.clear();
            nextToken = 1;
        }
        Choice choice = new Choice(nextToken++, entity, entityId, dimension, candidates, time + 60000L);
        pending.put(player, choice);
        return choice;
    }

    public synchronized Choice consume(UUID player, long token, long time) {
        Choice choice = pending.get(player);
        if (choice == null || choice.token != token) return null;
        pending.remove(player);
        return time > choice.expiresAt ? null : choice;
    }

    public synchronized void forget(UUID player) {
        pending.remove(player);
    }

    public static final class Choice {

        private final long token;
        private final UUID entity;
        private final int entityId;
        private final int dimension;
        private final List<CanonicalActorCandidate> candidates;
        private final long expiresAt;

        private Choice(long token, UUID entity, int entityId, int dimension, List<CanonicalActorCandidate> candidates,
            long expiresAt) {
            this.token = token;
            this.entity = entity;
            this.entityId = entityId;
            this.dimension = dimension;
            this.candidates = Collections.unmodifiableList(new ArrayList<CanonicalActorCandidate>(candidates));
            this.expiresAt = expiresAt;
        }

        public long getToken() {
            return token;
        }

        public UUID getEntity() {
            return entity;
        }

        public int getEntityId() {
            return entityId;
        }

        public int getDimension() {
            return dimension;
        }

        public List<CanonicalActorCandidate> getCandidates() {
            return candidates;
        }
    }
}
