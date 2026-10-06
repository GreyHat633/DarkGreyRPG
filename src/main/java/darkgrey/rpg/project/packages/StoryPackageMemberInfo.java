package darkgrey.rpg.project.packages;

import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;

/** Small immutable manager projection; no resource JSON, player state or media payloads. */
public final class StoryPackageMemberInfo {

    public final String uid, name, repeatPolicy, version, fingerprint, startRule;
    public final int ownedCount, referenceCount;

    StoryPackageMemberInfo(LoadedStoryPackage member) {
        uid = member.getStoryId();
        darkgrey.rpg.graph.canonical.CanonicalGraphResource story = member.getSnapshot()
            .getCanonicalStory(uid);
        name = story.getDisplayName();
        repeatPolicy = CanonicalStoryStartConfiguration.parse(story)
            .getRepeatPolicy()
            .name();
        java.util.Set<String> types = new java.util.LinkedHashSet<String>();
        for (CanonicalStoryStartConfiguration.Trigger trigger : CanonicalStoryStartConfiguration.parse(story)
            .getTriggers()) {
            String type = trigger.getType();
            types.add(
                "enter_region".equals(type) ? "进入区域"
                    : "interact_actor".equals(type) ? "交互角色"
                        : "flow_driven".equals(type) ? "流程触发" : "logic".equals(type) ? "逻辑条件" : type);
        }
        startRule = String.join("、", types);
        version = member.getManifest()
            .getProducerVersion();
        fingerprint = member.getContentFingerprint();
        ownedCount = count(
            member.getSnapshot()
                .getCanonicalStoryMembership(uid)
                .getOwnedResources());
        referenceCount = count(
            member.getSnapshot()
                .getCanonicalStoryMembership(uid)
                .getReferencedResources());
    }

    private static int count(CanonicalStoryMembershipSet resources) {
        return resources.getActors()
            .size()
            + resources.getItems()
                .size()
            + resources.getItemGroups()
                .size()
            + resources.getSessions()
                .size()
            + resources.getTasks()
                .size();
    }
}
