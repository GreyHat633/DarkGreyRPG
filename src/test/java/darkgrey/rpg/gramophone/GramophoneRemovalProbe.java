package darkgrey.rpg.gramophone;

import java.lang.reflect.*;
import java.util.*;
import java.util.concurrent.FutureTask;

/** Identity and pending-download cleanup through the production removal path. */
public final class GramophoneRemovalProbe {

    private static Field field(Class<?> type, String name) throws Exception {
        Field f = type.getDeclaredField(name);
        f.setAccessible(true);
        return f;
    }

    @SuppressWarnings("unchecked")
    static void run() throws Exception {
        Class<?> type = Class.forName("darkgrey.rpg.gramophone.GramophoneClient$Device");
        Constructor<?> constructor = type.getDeclaredConstructor(GramophonePacket.class);
        constructor.setAccessible(true);
        Class<?> mediaType = Class.forName("darkgrey.rpg.gramophone.GramophoneClient$Media");
        Constructor<?> mediaConstructor = mediaType.getDeclaredConstructor();
        mediaConstructor.setAccessible(true);
        Map<String, Object> devices = (Map<String, Object>) field(GramophoneClient.class, "DEVICES").get(null);
        Map<String, Object> cache = (Map<String, Object>) field(GramophoneClient.class, "MEDIA").get(null);
        List<Object> retiring = (List<Object>) field(GramophoneClient.class, "RETIRING").get(null);
        if (!devices.isEmpty() || !cache.isEmpty() || !retiring.isEmpty()) throw new AssertionError("isolated probe");
        GramophonePacket old = new GramophonePacket(), replacement = new GramophonePacket();
        old.instance = UUID.randomUUID()
            .toString();
        replacement.instance = UUID.randomUUID()
            .toString();
        old.source = "local:probe";
        Object device = constructor.newInstance(old), media = mediaConstructor.newInstance();
        FutureTask<Object> download = new FutureTask<Object>(() -> null);
        field(mediaType, "future").set(media, download);
        field(mediaType, "pins").setInt(media, 1);
        field(type, "media").set(device, media);
        cache.put(old.source, media);
        devices.put(replacement.key(), constructor.newInstance(replacement));
        retiring.add(device);
        Method remove = GramophoneClient.class.getDeclaredMethod("removeDevice", String.class);
        remove.setAccessible(true);
        try {
            remove.invoke(null, old.key());
            remove.invoke(null, old.key());
            if (!download.isCancelled() || !retiring.isEmpty()
                || !cache.isEmpty()
                || field(mediaType, "pins").getInt(media) != 0
                || !devices.containsKey(replacement.key()))
                throw new AssertionError("removed identity cleanup/replacement isolation");
        } finally {
            devices.clear();
            cache.clear();
            retiring.clear();
        }
        System.out.println("GRAMOPHONE_REMOVAL_IDENTITY_DOWNLOAD=PASS");
    }
}
