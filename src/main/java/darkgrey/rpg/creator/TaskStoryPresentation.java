package darkgrey.rpg.creator;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;

/** Story labels for presentation only; task and story identities are never rewritten. */
public final class TaskStoryPresentation {

    private TaskStoryPresentation() {}

    public static void fill(NBTTagCompound row, String story) {
        if (story == null || story.isEmpty()) return;
        row.setString("story", story);
        if (DarkGreyRpg.getProjectRepository() == null) return;
        CanonicalGraphResource resource = DarkGreyRpg.getProjectRepository()
            .getSnapshot()
            .getCanonicalStories()
            .get(story);
        if (resource != null) row.setString("story_title", resource.getDisplayName());
        if (DarkGreyRpg.getStoryPackageLoader() != null)
            for (darkgrey.rpg.project.packages.LoadedStoryPackage pack : DarkGreyRpg.getStoryPackageLoader()
                .getPackages()
                .values())
                if (pack.getSnapshot()
                    .getCanonicalStories()
                    .containsKey(story)) {
                        row.setString(
                            "story_package",
                            pack.getManifest()
                                .getPackageId());
                        break;
                    }
    }

    public static void recover(NBTTagCompound row) {
        String story = row.getString("story");
        darkgrey.rpg.identity.StoryUid.parse(story);
        if (row.getString("story_title")
            .isEmpty()) fill(row, story);
        else if (!story.isEmpty()) row.setString("story", story);
    }
}
