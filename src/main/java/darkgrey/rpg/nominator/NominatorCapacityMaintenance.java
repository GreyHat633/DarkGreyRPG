package darkgrey.rpg.nominator;

import java.lang.ref.WeakReference;
import java.util.Map;
import java.util.WeakHashMap;
import java.util.function.LongSupplier;

import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.identity.NpcIdentitySavedData;

/** Five-second server maintenance of already-loaded data; never opens or creates a SavedData file. */
public final class NominatorCapacityMaintenance {

    private static final Logger LOG = LogManager.getLogger(NominatorCapacityMaintenance.class);
    private static final Map<MapStorage, Tracked> LOADED = new WeakHashMap<MapStorage, Tracked>();
    private static final long CHECK_INTERVAL_NANOS = 5_000_000_000L;
    private final LongSupplier clock;
    private boolean checked;
    private long lastCheck;
    private String lastFailure;

    public NominatorCapacityMaintenance() {
        this(System::nanoTime);
    }

    NominatorCapacityMaintenance(LongSupplier clock) {
        this.clock = clock;
    }

    public static synchronized void track(MapStorage storage, NominatorSavedData data) {
        tracked(storage).selections = new WeakReference<NominatorSavedData>(data);
    }

    public static synchronized void track(MapStorage storage, NpcIdentitySavedData data) {
        tracked(storage).identities = new WeakReference<NpcIdentitySavedData>(data);
    }

    private static Tracked tracked(MapStorage storage) {
        Tracked value = LOADED.get(storage);
        if (value == null) {
            value = new Tracked();
            LOADED.put(storage, value);
        }
        return value;
    }

    public static synchronized void clear() {
        LOADED.clear();
    }

    @SubscribeEvent
    public void onServerTick(TickEvent.ServerTickEvent event) {
        if (event == null || event.phase != TickEvent.Phase.END) return;
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null) return;
        WorldServer overworld = server.worldServerForDimension(0);
        if (overworld == null) return;
        try {
            maintainLoaded(overworld.mapStorage);
            lastFailure = null;
        } catch (RuntimeException failure) {
            String message = failure.toString();
            if (!message.equals(lastFailure)) LOG.warn("Nominator memory maintenance deferred: {}", message);
            lastFailure = message;
        }
    }

    boolean maintainLoaded(MapStorage storage) {
        long now = clock.getAsLong();
        if (checked && now - lastCheck < CHECK_INTERVAL_NANOS) return false;
        checked = true;
        lastCheck = now;
        NominatorSavedData selections;
        NpcIdentitySavedData identities;
        synchronized (NominatorCapacityMaintenance.class) {
            Tracked tracked = LOADED.get(storage);
            if (tracked == null) return false;
            selections = tracked.selections.get();
            identities = tracked.identities.get();
        }
        if (identities != null) {
            synchronized (identities) {
                if (selections != null) {
                    synchronized (selections) {
                        if (!selections.readableForMaintenance()) return false;
                        return selections.maintainMemory() | identities.maintainMemory();
                    }
                }
                return identities.maintainMemory();
            }
        }
        if (selections != null) {
            synchronized (selections) {
                return selections.readableForMaintenance() && selections.maintainMemory();
            }
        }
        return false;
    }

    private static final class Tracked {

        private WeakReference<NominatorSavedData> selections = new WeakReference<NominatorSavedData>(null);
        private WeakReference<NpcIdentitySavedData> identities = new WeakReference<NpcIdentitySavedData>(null);
    }
}
