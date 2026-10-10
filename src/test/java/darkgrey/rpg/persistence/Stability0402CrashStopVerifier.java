package darkgrey.rpg.persistence;

import java.io.File;
import java.lang.reflect.Field;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Map;

import cpw.mods.fml.common.eventhandler.EventPriority;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

/** Explicit isolated crash fixture: exercise the stopped event when Forge skips stopping. */
public final class Stability0402CrashStopVerifier {

    private int ticks;

    @SubscribeEvent(priority = EventPriority.LOWEST)
    public void tick(TickEvent.ServerTickEvent event) throws Exception {
        if (event.phase != TickEvent.Phase.END || ++ticks != 240) return;
        Class<?> transactions = Class.forName("darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions");
        Field field = transactions.getDeclaredField("JOURNALS");
        field.setAccessible(true);
        int count = ((Map<?, ?>) field.get(null)).size();
        if (count == 0) throw new IllegalStateException("Expected populated transaction contexts before crash");
        File root = new File(System.getProperty("dgr0402.root"));
        Files.write(
            new File(root, "expected-crash-before.json").toPath(),
            ("{\"status\":\"ARMED_EXPECTED_CRASH\",\"journal_contexts_before\":" + count + "}")
                .getBytes(StandardCharsets.UTF_8));
        throw new LinkageError("DGR0402_EXPECTED_ISOLATED_CRASH_STOP");
    }
}
