package darkgrey.rpg.project.packages;

import java.io.File;

/** Reads the media archive produced by the actual Studio exporter test. */
public final class MediaPackage0330Probe {

    private MediaPackage0330Probe() {}

    public static void main(String[] args) throws Exception {
        StoryPackageLoader loader = new StoryPackageLoader(new File(args[0]));
        StoryPackageLoader.ReloadResult result = loader.reload();
        if (!result.isSuccessful()) throw new AssertionError(
            result.getErrors()
                .toString());
        LoadedStoryPackage story = loader.getPackages()
            .get("media_story");
        if (story == null || story.getManifest()
            .getRequiredResources()
            .getMedia()
            .size() != 1) throw new AssertionError("Missing reachable media package");
        String ref = story.getSnapshot()
            .getActor("hero")
            .getPortraits()
            .getDefaultRef();
        if (ref == null || story.getDeclaredResourceBytes(ref) == null)
            throw new AssertionError("Portrait payload missing");
        LoadedStoryPackage presentation = loader.getPackages()
            .get("presentation_story");
        if (presentation == null) throw new AssertionError("Missing actual Studio presentation package");
        darkgrey.rpg.session.runtime.CanonicalSessionRuntime runtime = darkgrey.rpg.session.runtime.CanonicalSessionRuntime
            .start(
                presentation.getSnapshot()
                    .getCanonicalSession("presentation_session"));
        if (runtime.snapshot()
            .getPresentation()
            .getLayers()
            .size() != 1
            || !"line".equals(
                runtime.snapshot()
                    .getCurrentNodeId()))
            throw new AssertionError("Music/screen graph did not load and execute");
        System.out.println("STUDIO_MEDIA_DGRS_JAVA_LOAD_AND_PORTRAIT=PASS");
    }
}
