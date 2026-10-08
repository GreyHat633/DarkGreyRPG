package darkgrey.rpg.project.packages;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.stream.Stream;

import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.project.ProjectLoadException;

/** Reads the same Studio-produced single/group bytes and rejects targeted mutations without rewriting them. */
public final class CanonicalOnly0400PackageProbe {

    private CanonicalOnly0400PackageProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args[0]);
        read(root.resolve("Exports/owner.dgrs"));
        read(root.resolve("Groups/current.dgrs.g"));
        int rejected = 0;
        try (Stream<Path> files = Files.list(root.resolve("RetiredPackages"))) {
            for (Path file : (Iterable<Path>) files.filter(Files::isRegularFile)::iterator) {
                byte[] original = Files.readAllBytes(file);
                try {
                    read(file);
                    throw new AssertionError("Native target package accepted: " + file);
                } catch (ProjectLoadException expected) {
                    Throwable cause = expected;
                    while (cause != null && !(cause instanceof CanonicalGraphResourceException))
                        cause = cause.getCause();
                    if (cause == null || !"graph.reference.identity.invalid"
                        .equals(((CanonicalGraphResourceException) cause).getCode()))
                        throw new AssertionError("Package failed for a different reason: " + file, expected);
                }
                if (!Arrays.equals(original, Files.readAllBytes(file)))
                    throw new AssertionError("Rejected archive was rewritten: " + file);
                Path install = Files.createTempDirectory(root, "rejection-install-");
                Files.copy(file, install.resolve(file.getFileName()));
                StoryPackageLoader loader = new StoryPackageLoader(
                    install.toFile(),
                    install.resolve("Cache")
                        .toFile());
                if (loader.reload()
                    .isSuccessful()
                    || !loader.getPackages()
                        .isEmpty())
                    throw new AssertionError("Rejected container partially registered: " + file);
                rejected++;
            }
        }
        if (rejected != 6) throw new AssertionError("Expected all six single/group native target vectors: " + rejected);
        int contracts = 0;
        try (Stream<Path> files = Files.list(root.resolve("RetiredContracts"))) {
            for (Path file : (Iterable<Path>) files.filter(Files::isRegularFile)::iterator) {
                byte[] original = Files.readAllBytes(file);
                try {
                    read(file);
                    throw new AssertionError("Retired Manifest accepted: " + file);
                } catch (ProjectLoadException expected) {
                    if (!expected.getMessage()
                        .contains("field")
                        && !expected.getMessage()
                            .contains("version"))
                        throw new AssertionError("Manifest failed for a different reason: " + file, expected);
                }
                if (!Arrays.equals(original, Files.readAllBytes(file)))
                    throw new AssertionError("Manifest rejection rewrote archive: " + file);
                Path install = Files.createTempDirectory(root, "contract-install-");
                Files.copy(file, install.resolve(file.getFileName()));
                StoryPackageLoader loader = new StoryPackageLoader(
                    install.toFile(),
                    install.resolve("Cache")
                        .toFile());
                if (loader.reload()
                    .isSuccessful()
                    || !loader.getPackages()
                        .isEmpty())
                    throw new AssertionError("Manifest rejection partially installed: " + file);
                contracts++;
            }
        }
        if (contracts != 15) throw new AssertionError("Expected fifteen Manifest single/group vectors: " + contracts);
        System.out.println(
            "CANONICAL_0400_STUDIO_SINGLE_GROUP_NATIVE_REJECTION_NO_REWRITE_NO_PARTIAL_INSTALL=PASS vectors="
                + rejected);
        System.out.println(
            "MANIFEST_0400_SAME_STUDIO_SINGLE_GROUP_RETIRED_FIELDS_VERSIONS_ATOMIC_REJECT=PASS vectors=" + contracts);
    }

    private static void read(Path path) throws ProjectLoadException {
        DgrsArchiveReader archive = DgrsArchiveReader.open(path.toFile());
        if (path.toString()
            .endsWith(".dgrs.g")) StoryGroupPackageReader.read(archive);
        else StoryPackageSnapshotReader
            .read(archive, StoryPackageManifest.read(archive.readBytes("manifest.json"), path.toString()));
    }
}
