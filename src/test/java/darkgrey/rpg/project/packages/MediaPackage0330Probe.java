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
        if (ref == null) throw new AssertionError("Portrait payload missing");
        try (LoadedStoryPackage.MediaReader reader = story.openMediaReader(ref)) {
            if (reader == null) throw new AssertionError("Portrait reader could not be opened");
            darkgrey.rpg.network.message.canonical.CanonicalMediaChunk first = reader.readChunk(901L, 0);
            if (first == null || first.getTotal() <= 0 || first.getData().length != Math.min(first.getTotal(), 32768))
                throw new AssertionError("Portrait reader did not return the first bounded chunk");
            if (reader.readChunk(901L, 1) != null)
                throw new AssertionError("Portrait reader accepted an unaligned final offset");
            java.security.MessageDigest digest = java.security.MessageDigest.getInstance("SHA-256");
            for (int offset = 0; offset < first.getTotal(); offset += 32768) {
                darkgrey.rpg.network.message.canonical.CanonicalMediaChunk chunk = reader.readChunk(901L, offset);
                if (chunk == null || chunk.getOffset() != offset || chunk.getTotal() != first.getTotal())
                    throw new AssertionError("Reader lost media bytes");
                digest.update(chunk.getData());
            }
            StringBuilder actual = new StringBuilder();
            for (byte value : digest.digest()) actual.append(String.format("%02x", value & 255));
            if (!ref.substring(6, 70)
                .equals(actual.toString())) throw new AssertionError("Reader changed media content");
        }
        if (!story.getSourceArchive()
            .isFile()) throw new AssertionError("Package source archive disappeared");
        StoryPackageLoader.ReloadResult readerLifecycle = loader.reload();
        if (!readerLifecycle.isSuccessful())
            throw new AssertionError("Package reload after reader close failed: " + readerLifecycle.getSummary());
        System.out.println("DGRS_MEDIA_RETAINED_READER_LEASE_CLOSE_RELOAD=PASS");
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
