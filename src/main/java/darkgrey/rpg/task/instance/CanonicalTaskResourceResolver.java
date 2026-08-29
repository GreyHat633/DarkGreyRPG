package darkgrey.rpg.task.instance;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;

/** Resolves a persisted canonical Task resource without coupling the store to Forge. */
public interface CanonicalTaskResourceResolver {

    CanonicalGraphResource resolve(String taskResourceId);
}
