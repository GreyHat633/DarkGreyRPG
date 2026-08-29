package darkgrey.rpg.story.canonical.instance;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;

/** Resolves the current canonical Story resource while restoring persisted instances. */
public interface CanonicalStoryResourceResolver {

    CanonicalGraphResource resolve(String storyResourceId);
}
