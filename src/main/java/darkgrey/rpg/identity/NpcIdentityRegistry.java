package darkgrey.rpg.identity;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.regex.Pattern;

/** Server-authoritative one-to-one mapping between DGR NPC IDs and real hosts. */
public final class NpcIdentityRegistry {

    private static final Pattern ID = Pattern.compile("[a-z0-9][a-z0-9_-]*");
    private final Map<String, NpcHostIdentity> byNpcId = new LinkedHashMap<String, NpcHostIdentity>();
    private final Map<UUID, String> byHostUuid = new LinkedHashMap<UUID, String>();

    /**
     * Binds a free NPC ID to a free host. Repeating the exact operation is
     * idempotent; every conflicting operation fails without mutation.
     */
    public synchronized boolean bind(String npcId, NpcHostIdentity host) {
        String validId = requireId(npcId);
        if (host == null) throw new IllegalArgumentException("NPC host is required.");
        NpcHostIdentity existingHost = byNpcId.get(validId);
        String existingId = byHostUuid.get(host.getEntityUuid());
        if (existingHost != null || existingId != null) {
            if (host.equals(existingHost) && validId.equals(existingId)) return false;
            if (existingHost != null)
                throw new NpcIdentityConflictException("NPC ID '" + validId + "' is already occupied.");
            throw new NpcIdentityConflictException(
                "Entity '" + host.getEntityUuid() + "' already hosts NPC ID '" + existingId + "'.");
        }
        byNpcId.put(validId, host);
        byHostUuid.put(host.getEntityUuid(), validId);
        return true;
    }

    /** Explicitly migrates an occupied ID to a replacement entity identity. */
    public synchronized boolean transfer(String npcId, NpcHostIdentity replacement) {
        String validId = requireId(npcId);
        if (replacement == null) throw new IllegalArgumentException("Replacement NPC host is required.");
        NpcHostIdentity previous = byNpcId.get(validId);
        if (previous == null) throw new IllegalArgumentException("NPC ID '" + validId + "' is not bound.");
        if (previous.equals(replacement)) return false;
        String occupied = byHostUuid.get(replacement.getEntityUuid());
        if (occupied != null && !occupied.equals(validId))
            throw new NpcIdentityConflictException("Replacement entity already hosts NPC ID '" + occupied + "'.");
        byHostUuid.remove(previous.getEntityUuid());
        byNpcId.put(validId, replacement);
        byHostUuid.put(replacement.getEntityUuid(), validId);
        return true;
    }

    /** Updates diagnostics for the same UUID without releasing the unique ID. */
    public synchronized boolean observe(NpcHostIdentity observed) {
        if (observed == null) throw new IllegalArgumentException("Observed NPC host is required.");
        String npcId = byHostUuid.get(observed.getEntityUuid());
        if (npcId == null) return false;
        NpcHostIdentity previous = byNpcId.get(npcId);
        if (observed.equals(previous)) return false;
        byNpcId.put(npcId, observed);
        return true;
    }

    public synchronized boolean unbindNpcId(String npcId) {
        String validId = requireId(npcId);
        NpcHostIdentity removed = byNpcId.remove(validId);
        if (removed == null) return false;
        byHostUuid.remove(removed.getEntityUuid());
        return true;
    }

    public synchronized boolean unbindHost(UUID hostUuid) {
        if (hostUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        String npcId = byHostUuid.remove(hostUuid);
        if (npcId == null) return false;
        byNpcId.remove(npcId);
        return true;
    }

    public synchronized NpcHostIdentity getHost(String npcId) {
        return byNpcId.get(requireId(npcId));
    }

    public synchronized String getNpcId(UUID hostUuid) {
        if (hostUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        return byHostUuid.get(hostUuid);
    }

    public synchronized int size() {
        return byNpcId.size();
    }

    public synchronized List<Binding> bindings() {
        List<Binding> result = new ArrayList<Binding>();
        for (Map.Entry<String, NpcHostIdentity> entry : byNpcId.entrySet())
            result.add(new Binding(entry.getKey(), entry.getValue()));
        Collections.sort(result, new Comparator<Binding>() {

            @Override
            public int compare(Binding left, Binding right) {
                return left.getNpcId()
                    .compareTo(right.getNpcId());
            }
        });
        return Collections.unmodifiableList(result);
    }

    public synchronized void replaceAll(List<Binding> replacement) {
        if (replacement == null) throw new IllegalArgumentException("NPC bindings are required.");
        NpcIdentityRegistry candidate = new NpcIdentityRegistry();
        for (Binding binding : replacement) {
            if (binding == null) throw new IllegalArgumentException("NPC binding cannot be null.");
            if (!candidate.bind(binding.getNpcId(), binding.getHost())) throw new NpcIdentityConflictException(
                "Duplicate persisted NPC binding for '" + binding.getNpcId() + "'.");
        }
        byNpcId.clear();
        byHostUuid.clear();
        for (Binding binding : candidate.bindings()) {
            byNpcId.put(binding.getNpcId(), binding.getHost());
            byHostUuid.put(
                binding.getHost()
                    .getEntityUuid(),
                binding.getNpcId());
        }
    }

    public static String requireId(String npcId) {
        if (npcId == null || !ID.matcher(npcId)
            .matches()) throw new IllegalArgumentException("NPC ID must match [a-z0-9][a-z0-9_-]*.");
        return npcId;
    }

    public static final class Binding {

        private final String npcId;
        private final NpcHostIdentity host;

        public Binding(String npcId, NpcHostIdentity host) {
            this.npcId = requireId(npcId);
            if (host == null) throw new IllegalArgumentException("NPC host is required.");
            this.host = host;
        }

        public String getNpcId() {
            return npcId;
        }

        public NpcHostIdentity getHost() {
            return host;
        }
    }
}
