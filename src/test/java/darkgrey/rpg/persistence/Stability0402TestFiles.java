package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;

/** Atomic, bounded publication of isolated test output on Windows. */
public final class Stability0402TestFiles {

    private Stability0402TestFiles() {}

    public static void write(File root, String name, String text) throws Exception {
        Path target = new File(root, name).toPath();
        Path temporary = new File(root, name + ".tmp").toPath();
        Files.write(temporary, text.getBytes(StandardCharsets.UTF_8));
        for (int attempt = 0;; attempt++) {
            try {
                Files.move(temporary, target, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
                if (attempt > 0)
                    System.out.println("DGR0402_TEST_ATOMIC_WRITE_RETRIED file=" + name + " attempts=" + attempt);
                return;
            } catch (java.nio.file.FileSystemException sharingConflict) {
                if (attempt >= 19) throw sharingConflict;
                java.util.concurrent.locks.LockSupport.parkNanos(1000000L);
            }
        }
    }
}
