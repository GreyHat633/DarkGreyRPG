package darkgrey.rpg.diagnostics;

import java.lang.reflect.Field;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.client.gui.GuiPlayerStateInspection;

/** Exercises the production GUI response receiver, without rendering or a fake network transport. */
public final class InspectionClient0334Probe {

    public static void main(String[] args) throws Exception {
        GuiPlayerStateInspection screen = new GuiPlayerStateInspection();
        pending(screen, 1);
        NBTTagCompound first = response("first");
        screen.accept(1, first);
        first.setString("name", "mutated sender");
        check(screen, "first", false);
        pending(screen, 2);
        screen.accept(1, response("stale"));
        check(screen, "first", true);
        screen.accept(3, response("unsolicited future"));
        check(screen, "first", true);
        screen.accept(2, response("second"));
        check(screen, "second", false);
        screen.accept(2, response("duplicate"));
        check(screen, "second", false);
        // The actual receiver must reject late results after timeout has cleared loading.
        pending(screen, 3);
        field("loading").setBoolean(screen, false);
        screen.accept(3, response("late after timeout"));
        check(screen, "second", false);
        GuiPlayerStateInspection reopened = new GuiPlayerStateInspection();
        pending(reopened, 4);
        reopened.accept(3, response("previous window"));
        check(reopened, "", true);
        reopened.accept(4, response("reopened"));
        check(reopened, "reopened", false);
        long started = System.nanoTime();
        for (int i = 5; i < 10005; i++) {
            pending(reopened, i);
            reopened.accept(i - 1, response("old"));
            reopened.accept(i, response("current-" + i));
            reopened.accept(i - 1, response("old after current"));
            check(reopened, "current-" + i, false);
        }
        System.out.println(
            "InspectionClient0334Probe PASS: production GUI receiver stale/future/duplicate/timeout/reopened isolation and detached copy; 10000 rapid reordered cycles ms="
                + (System.nanoTime() - started) / 1000000.0
                + "; excludes socket and rendering");
    }

    private static Field field(String name) throws Exception {
        Field field = GuiPlayerStateInspection.class.getDeclaredField(name);
        field.setAccessible(true);
        return field;
    }

    private static void pending(GuiPlayerStateInspection screen, long request) throws Exception {
        field("request").setLong(screen, request);
        field("loading").setBoolean(screen, true);
    }

    private static NBTTagCompound response(String name) {
        NBTTagCompound data = new NBTTagCompound();
        data.setString("name", name);
        return data;
    }

    private static void check(GuiPlayerStateInspection screen, String name, boolean loading) throws Exception {
        if (!name.equals(((NBTTagCompound) field("snapshot").get(screen)).getString("name"))
            || field("loading").getBoolean(screen) != loading)
            throw new AssertionError("Response correlation: " + name);
    }
}
