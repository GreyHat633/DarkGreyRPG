package darkgrey.rpg.client;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Base64;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.client.gui.UtilityWindowChrome;
import darkgrey.rpg.client.session.PlayerReadingContext;

public final class TaskTrackerClient {

    private static String context;
    private static TaskTrackingSelection selection = new TaskTrackingSelection();
    private static long revision;

    private TaskTrackerClient() {}

    public static long revision() {
        return revision;
    }

    public static Set<String> selected() {
        return selection.selected();
    }

    private static String key() {
        return "tracking." + Base64.getUrlEncoder()
            .withoutPadding()
            .encodeToString(context.getBytes(StandardCharsets.UTF_8));
    }

    private static void save() {
        if (context == null) return;
        List<String> encoded = new ArrayList<String>();
        for (String id : selected()) encoded.add(
            Base64.getUrlEncoder()
                .withoutPadding()
                .encodeToString(id.getBytes(StandardCharsets.UTF_8)));
        try {
            UtilityWindowChrome.settings()
                .savePreference(key(), String.join(",", encoded));
        } catch (RuntimeException error) {
            darkgrey.rpg.DarkGreyRpg.LOG.warn("Could not save task tracking", error);
        }
    }

    public static void accept(NBTTagCompound snapshot, boolean initial) {
        String next = PlayerReadingContext.current();
        if (!java.util.Objects.equals(context, next)) {
            context = next;
            selection = new TaskTrackingSelection();
            revision++;
            if (next != null) for (String encoded : UtilityWindowChrome.settings()
                .preference(key())
                .split(",")) {
                    try {
                        selection.track(
                            new String(
                                Base64.getUrlDecoder()
                                    .decode(encoded),
                                StandardCharsets.UTF_8));
                    } catch (IllegalArgumentException invalid) { /* Ignore only the malformed identity. */ }
                }
        }
        Set<String> before = selected();
        Set<String> active = new LinkedHashSet<String>();
        NBTTagList tasks = snapshot.getTagList("tasks", 10);
        for (int i = 0; i < tasks.tagCount(); i++) active.add(identity(tasks.getCompoundTagAt(i)));
        List<String> received = new ArrayList<String>();
        if (!initial) {
            NBTTagList events = snapshot.getTagList("notifications", 10);
            for (int i = 0; i < events.tagCount(); i++) {
                NBTTagCompound event = events.getCompoundTagAt(i);
                if ("received".equals(event.getString("kind"))) received.add(event.getString("task"));
            }
        }
        selection.reconcile(active, received);
        if (!before.equals(selected())) {
            revision++;
            save();
        }
    }

    public static String identity(NBTTagCompound task) {
        return task.getString("id") + ":" + task.getLong("activation");
    }

    public static boolean toggle(NBTTagCompound task) {
        String id = identity(task);
        if (selected().contains(id)) selection.untrack(id);
        else if (!selection.track(id)) return false;
        revision++;
        save();
        return true;
    }
}
