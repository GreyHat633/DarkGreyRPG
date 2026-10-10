package darkgrey.rpg.persistence;

import java.lang.reflect.Field;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;

/** Fails on whole-world traversal while allowing authoritative per-identity lookup and commit. */
public final class Scoped0402TraversalGuard extends LinkedHashMap<Object, Object> implements AutoCloseable {

    private boolean guarded = true;

    @SuppressWarnings("unchecked")
    public static Scoped0402TraversalGuard install(Object store) throws Exception {
        Field field = store.getClass()
            .getDeclaredField("instances");
        field.setAccessible(true);
        Scoped0402TraversalGuard guard = new Scoped0402TraversalGuard((Map<Object, Object>) field.get(store));
        field.set(store, guard);
        return guard;
    }

    private Scoped0402TraversalGuard(Map<Object, Object> original) {
        super(original);
    }

    @Override
    public Collection<Object> values() {
        if (guarded) throw new AssertionError("Single-instance operation traversed all world instances");
        return super.values();
    }

    @Override
    public Set<Map.Entry<Object, Object>> entrySet() {
        if (guarded) throw new AssertionError("Single-instance operation encoded all world instances");
        return super.entrySet();
    }

    @Override
    public void close() {
        guarded = false;
    }
}
