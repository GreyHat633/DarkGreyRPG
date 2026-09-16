package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

public final class PackageRuntimeMigrationPlanProbe {

    public static void main(String[] args) throws Exception {
        Path root = Files.createTempDirectory(Paths.get(args[0]), "runtime-migration-");
        Path install = Files.createDirectories(root.resolve("StoryPackages"));
        Path cache = root.resolve("Cache");
        Path target = PackageRuntimeMigration.cacheRoot(install.toFile(), cache.toFile());
        require(target.startsWith(cache.resolve("StoryPackagesRuntime")), "owned cache root");
        require(
            !target.equals(
                PackageRuntimeMigration.cacheRoot(
                    root.resolve("OtherPackages")
                        .toFile(),
                    cache.toFile())),
            "installation isolation");
        Path legacy = Files.createDirectories(install.resolve(".dgrs-runtime/media"));
        byte[] payload = "hello".getBytes(StandardCharsets.UTF_8);
        String name = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824.png";
        Files.write(legacy.resolve(name), payload);
        Files.write(legacy.resolve("user.txt"), payload);
        List<String> diagnostics = new ArrayList<String>();
        PackageRuntimeMigration.migrate(install, target, diagnostics);
        require(
            Files.exists(
                target.resolve("media")
                    .resolve(name)),
            "verified managed media moved");
        require(Files.exists(legacy.resolve("user.txt")), "unknown data preserved");
        require(!diagnostics.isEmpty(), "unknown diagnostic");
        Files.write(legacy.resolve(name), payload);
        diagnostics.clear();
        PackageRuntimeMigration.migrate(install, target, diagnostics);
        require(Files.exists(legacy.resolve(name)), "conflicting source preserved");
        require(!diagnostics.isEmpty(), "conflict diagnostic");
        System.out.println("PACKAGE_RUNTIME_MIGRATION_PLAN_PROBE=PASS " + root);
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
