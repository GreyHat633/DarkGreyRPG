package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.List;

/** Moves only verified, program-owned immutable artifacts. Unknown contents are preserved. */
final class PackageRuntimeMigration {

    private PackageRuntimeMigration() {}

    static Path cacheRoot(File install, File cache) {
        try {
            String identity = install.getCanonicalPath();
            if (File.separatorChar == '\\') identity = identity.toLowerCase(java.util.Locale.ROOT);
            byte[] digest = MessageDigest.getInstance("SHA-256")
                .digest(identity.getBytes(StandardCharsets.UTF_8));
            StringBuilder key = new StringBuilder();
            for (byte value : digest) key.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
            return cache.toPath()
                .toAbsolutePath()
                .normalize()
                .resolve("StoryPackagesRuntime")
                .resolve(key.toString());
        } catch (IOException | java.security.NoSuchAlgorithmException e) {
            throw new IllegalArgumentException("Cannot identify package installation", e);
        }
    }

    static void migrate(Path install, Path destination, List<String> diagnostics) {
        Path legacy = install.resolve(".dgrs-runtime")
            .toAbsolutePath()
            .normalize();
        if (!Files.exists(legacy, LinkOption.NOFOLLOW_LINKS)) return;
        try {
            safeDirectory(legacy);
            for (String folder : new String[] { "generations", "media" }) {
                Path source = legacy.resolve(folder);
                if (!Files.exists(source, LinkOption.NOFOLLOW_LINKS)) continue;
                safeDirectory(source);
                try (DirectoryStream<Path> files = Files.newDirectoryStream(source)) {
                    for (Path file : files) {
                        try {
                            String name = file.getFileName()
                                .toString();
                            String pattern = folder.equals("generations") ? "[0-9a-f]{64}\\.dgrs"
                                : "[0-9a-f]{64}\\.(png|jpg|ogg)";
                            if (!name.matches(pattern) || !Files.isRegularFile(file, LinkOption.NOFOLLOW_LINKS)
                                || !file.toRealPath()
                                    .equals(
                                        file.toAbsolutePath()
                                            .normalize()))
                                throw new IOException("Unrecognized or linked cache entry");
                            if (folder.equals("generations")) {
                                DgrsArchiveReader reader = DgrsArchiveReader.open(file.toFile());
                                StoryPackageManifest manifest = StoryPackageManifest
                                    .read(reader.readBytes("manifest.json"), file.toString());
                                if (!name.substring(0, 64)
                                    .equals(StoryPackageContentFingerprint.compute(manifest, reader)))
                                    throw new IOException("Archive fingerprint mismatch");
                            } else {
                                MessageDigest digest = MessageDigest.getInstance("SHA-256");
                                try (java.io.InputStream input = Files.newInputStream(file)) {
                                    byte[] buffer = new byte[32768];
                                    int count;
                                    while ((count = input.read(buffer)) != -1) digest.update(buffer, 0, count);
                                }
                                StringBuilder hash = new StringBuilder();
                                for (byte value : digest.digest())
                                    hash.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
                                if (!name.substring(0, 64)
                                    .equals(hash.toString())) throw new IOException("Media fingerprint mismatch");
                            }
                            Path targetFolder = destination.resolve(folder);
                            Files.createDirectories(targetFolder);
                            safeDirectory(targetFolder);
                            Files.move(file, targetFolder.resolve(name)); // no replacement, including on conflict
                        } catch (Exception e) {
                            diagnostics.add("Preserved legacy cache " + file + ": " + e.getMessage());
                        }
                    }
                }
                deleteEmpty(source);
            }
            try (DirectoryStream<Path> remaining = Files.newDirectoryStream(legacy)) {
                for (Path entry : remaining) diagnostics.add("Preserved legacy runtime contents: " + entry);
            }
            deleteEmpty(legacy);
        } catch (IOException e) {
            diagnostics.add("Cannot migrate legacy runtime " + legacy + ": " + e.getMessage());
        }
    }

    private static void safeDirectory(Path path) throws IOException {
        if (!Files.isDirectory(path, LinkOption.NOFOLLOW_LINKS) || !path.toRealPath()
            .equals(
                path.toAbsolutePath()
                    .normalize()))
            throw new IOException("Unsafe cache directory: " + path);
    }

    private static void deleteEmpty(Path path) throws IOException {
        try (DirectoryStream<Path> entries = Files.newDirectoryStream(path)) {
            if (entries.iterator()
                .hasNext()) return;
        }
        Files.delete(path);
    }
}
