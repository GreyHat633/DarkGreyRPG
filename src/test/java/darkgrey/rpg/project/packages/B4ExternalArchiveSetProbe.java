package darkgrey.rpg.project.packages;

import java.io.File;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.Map;

/** Reads genuine Studio-exported archives; every mutation stays in a fresh probe install directory. */
public final class B4ExternalArchiveSetProbe {

    private B4ExternalArchiveSetProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = new File(args[0]).getCanonicalFile()
            .toPath();
        Path allowed = new File(".tooling").getCanonicalFile()
            .toPath();
        require(root.startsWith(allowed), "Fixture root must stay inside repository .tooling");
        Path install = Files.createTempDirectory(root, "installed-");
        Path consumer = install.resolve("consumer.dgrs");
        Path provider = install.resolve("provider.dgrs");
        Files.copy(root.resolve("consumer.dgrs"), consumer);
        StoryPackageLoader loader = new StoryPackageLoader(install.toFile());
        require(
            !loader.reload()
                .isSuccessful(),
            "Missing provider must reject cold startup");
        require(
            loader.getPackages()
                .isEmpty(),
            "Failed cold startup published a partial set");
        Files.copy(root.resolve("provider.dgrs"), provider);
        StoryPackageLoader.ReloadResult result = loader.reload();
        require(result.isSuccessful(), "Paired archives rejected: " + result.getErrors());
        require(
            loader.getPackages()
                .size() == 2,
            "Both same-local Story packages must coexist");
        Map<String, LoadedStoryPackage> accepted = loader.getPackages();
        darkgrey.rpg.project.ProjectSnapshot merged = StoryPackageSnapshotMerger.merge(accepted);
        require(
            merged.getActors()
                .containsKey("Provider:guard")
                && merged.getActors()
                    .containsKey("Consumer:guard"),
            "Same local Actor IDs from two namespaces collided");
        Files.delete(provider);
        require(
            !loader.reload()
                .isSuccessful(),
            "Removing required provider was accepted");
        require(
            loader.getPackages()
                .equals(accepted),
            "Missing provider replaced last-known-good definitions");
        Files.copy(root.resolve("provider.dgrs"), provider);
        Files.copy(root.resolve("conflict.dgrs"), consumer, StandardCopyOption.REPLACE_EXISTING);
        require(
            !loader.reload()
                .isSuccessful(),
            "Different bytes for same full Actor ID were accepted");
        require(
            loader.getPackages()
                .equals(accepted),
            "Conflict replaced last-known-good definitions");
        verifyOptionalCaseSensitiveArchive(root);
        System.out.println("STUDIO_DGRS_EXTERNAL_REFERENCE_MERGE=PASS");
        System.out.println("DGRS_SAME_LOCAL_DIFFERENT_NAMESPACE_COEXIST=PASS");
        System.out.println("DGRS_SAME_FULL_ID_DIFFERENT_DEFINITION_CONFLICT=PASS");
        System.out.println("DGRS_MISSING_PROVIDER_AND_CONFLICT_PRESERVE_LKG=PASS");
    }

    /**
     * When supplied by the B4 acceptance harness, loads the genuine Studio
     * archive and proves that same-namespace case variants survive the full
     * StoryPackageLoader and canonical merge path. The default probe remains
     * fixture-only when the property/environment variable is absent.
     */
    private static void verifyOptionalCaseSensitiveArchive(Path probeRoot) throws Exception {
        String configured = System.getProperty("DGR_B4_CASE_ARCHIVE");
        if (configured == null || configured.trim()
            .isEmpty()) configured = System.getenv("DGR_B4_CASE_ARCHIVE");
        if (configured == null || configured.trim()
            .isEmpty()) return;

        Path tooling = new File(".tooling").getCanonicalFile()
            .toPath()
            .toAbsolutePath()
            .normalize();
        Path archive = Paths.get(configured)
            .toAbsolutePath()
            .normalize();
        require(archive.startsWith(tooling), "Case-sensitive archive must stay under repository .tooling");
        require(Files.isRegularFile(archive), "Configured case-sensitive archive is missing: " + archive);

        Path install = Files.createTempDirectory(probeRoot, "case-installed-");
        Files.copy(archive, install.resolve(archive.getFileName()));
        StoryPackageLoader loader = new StoryPackageLoader(install.toFile());
        StoryPackageLoader.ReloadResult result = loader.reload();
        require(result.isSuccessful(), "Case-sensitive Studio archive rejected: " + result.getErrors());
        require(
            loader.getPackages()
                .size() == 1,
            "Case-sensitive Studio archive count changed");

        darkgrey.rpg.project.ProjectSnapshot merged = StoryPackageSnapshotMerger.merge(loader.getPackages());
        require(
            merged.getCanonicalStory("Team:CaseStory") != null || merged.getStory("Team:CaseStory") != null,
            "Case-sensitive Story ID was not retained");
        require(
            merged.getActors()
                .containsKey("Team:Guard")
                && merged.getActors()
                    .containsKey("Team:guard"),
            "Case-sensitive Actor IDs did not coexist after archive merge");
        require(
            merged.getActors()
                .containsKey("Team:GuardGroup")
                && merged.getActors()
                    .containsKey("Team:guardGroup"),
            "Case-sensitive collective Actor IDs did not coexist after archive merge");
        require(
            merged.getItems()
                .containsKey("Team:Token")
                && merged.getItems()
                    .containsKey("Team:token"),
            "Case-sensitive Item IDs did not coexist after archive merge");
        require(
            merged.getItemGroups()
                .containsKey("Team:Tokens")
                && merged.getItemGroups()
                    .containsKey("Team:tokens"),
            "Case-sensitive Item Group IDs did not coexist after archive merge");
        System.out.println("STUDIO_DGRS_CASE_SENSITIVE_ARCHIVE=PASS");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
