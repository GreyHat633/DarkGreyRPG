package darkgrey.rpg.session.instance;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;

/** Resolves persisted Session resource IDs without coupling the store to a repository. */
public interface CanonicalSessionResourceResolver {

    CanonicalGraphResource resolve(String sessionResourceId);
}
