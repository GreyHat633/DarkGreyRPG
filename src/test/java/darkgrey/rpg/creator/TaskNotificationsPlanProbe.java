package darkgrey.rpg.creator;

import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

public final class TaskNotificationsPlanProbe {

    private static final UUID PLAYER = new UUID(1, 2);

    public static void main(String[] args) throws Exception {
        CanonicalTaskNotifications events = new CanonicalTaskNotifications();
        require(
            events.update(Collections.<CanonicalTaskJournalEntry>emptyList(), true)
                .tagCount() == 0,
            "initial empty");
        CanonicalTaskJournalEntry active = task(
            CanonicalTaskInstanceStatus.ACTIVE,
            CanonicalTaskObjectiveStatus.ACTIVE,
            0);
        NBTTagList received = events.update(Collections.singletonList(active), false);
        require(received.tagCount() == 2, "task and new objective");
        require(
            !received.getCompoundTagAt(0)
                .getString("event")
                .isEmpty(),
            "stable event identity");
        require(
            events.update(Collections.singletonList(active), false)
                .tagCount() == 0,
            "repeat suppressed");
        require(
            events
                .update(
                    Collections.singletonList(
                        task(CanonicalTaskInstanceStatus.ACTIVE, CanonicalTaskObjectiveStatus.ACTIVE, 1)),
                    false)
                .tagCount() == 0,
            "counter increment silent");
        NBTTagList completed = events.update(
            Collections
                .singletonList(task(CanonicalTaskInstanceStatus.SETTLED, CanonicalTaskObjectiveStatus.COMPLETED, 2)),
            false);
        require(completed.tagCount() == 2, "objective and task complete");
        require(
            events.update(Collections.singletonList(active), true)
                .tagCount() == 0,
            "reconnect baseline silent");
        require(
            events
                .update(
                    Collections
                        .singletonList(task(CanonicalTaskInstanceStatus.ERROR, CanonicalTaskObjectiveStatus.ACTIVE, 0)),
                    false)
                .tagCount() == 1,
            "task failure");
        clientChecks();
        System.out.println("TASK_NOTIFICATIONS_PLAN_PROBE=PASS");
    }

    private static Object field(Object object, String name) throws Exception {
        java.lang.reflect.Field f = object.getClass()
            .getDeclaredField(name);
        f.setAccessible(true);
        return f.get(object);
    }

    private static Map<?, ?> state(String name) throws Exception {
        java.lang.reflect.Field f = darkgrey.rpg.client.TaskNotificationCards.class.getDeclaredField(name);
        f.setAccessible(true);
        return (Map<?, ?>) f.get(null);
    }

    private static double number(Object card, String method, long now) throws Exception {
        java.lang.reflect.Method m = card.getClass()
            .getDeclaredMethod(method, long.class);
        m.setAccessible(true);
        return ((Number) m.invoke(card, now)).doubleValue();
    }

    private static net.minecraft.nbt.NBTTagCompound event(String id, String task, String kind, String objective) {
        net.minecraft.nbt.NBTTagCompound e = new net.minecraft.nbt.NBTTagCompound();
        e.setString("event", id);
        e.setString("task", task);
        e.setString("kind", kind);
        e.setString("objective", objective);
        e.setString("text", objective);
        e.setString("title", "任务名称");
        return e;
    }

    private static void accept(long now, net.minecraft.nbt.NBTTagCompound... events) {
        NBTTagList list = new NBTTagList();
        for (net.minecraft.nbt.NBTTagCompound e : events) list.appendTag(e);
        darkgrey.rpg.client.TaskNotificationCards.accept(list, now);
    }

    private static void clientChecks() throws Exception {
        for (boolean reverse : new boolean[] { false, true }) {
            darkgrey.rpg.client.TaskNotificationCards.clear();
            net.minecraft.nbt.NBTTagCompound old = event("a", "t", "objective_completed", "旧目标");
            net.minecraft.nbt.NBTTagCompound next = event("b", "t", "objective_active", "新目标");
            accept(0, reverse ? next : old, reverse ? old : next);
            Object card = state("cards").get("t");
            require(((Map<?, ?>) field(card, "active")).containsKey("新目标"), "order independent next step");
            require(field(card, "completed").equals("旧目标"), "structured completion");
            accept(100000000L, event("c", "t", "objective_active", "并行目标"));
            require(((Map<?, ?>) field(card, "active")).size() == 2, "multiple objectives");
            require(((Long) field(card, "entered")) == 0, "update preserves entry");
            accept(200000000L, event("d", "t", "completed", ""));
            require(
                ((Map<?, ?>) field(card, "active")).isEmpty() && field(card, "completed").equals(""),
                "terminal clears objectives");
            accept(300000000L, event("d", "t", "completed", ""));
            require(((Long) field(card, "exitAt")) == 4600000000L, "duplicate does not extend reading");
        }
        darkgrey.rpg.client.TaskNotificationCards.clear();
        accept(0, event("1", "t", "received", ""));
        Object card = state("cards").get("t");
        require(number(card, "offset", 0) == 1 && number(card, "offset", 600000000L) == 0, "600ms entry");
        require(number(card, "offset", 4600000000L) == 0, "four full seconds of reading");
        require(Math.abs(number(card, "offset", 4900000000L) - 0.5) < 0.001, "exit halfway");
        double before = number(card, "offset", 4900000000L);
        accept(4900000000L, event("2", "t", "objective_active", "新目标"));
        require(number(card, "offset", 4900000000L) == before, "exit reversal has no position jump");
        require(number(card, "offset", 5500000000L) == 0, "returns smoothly");
        accept(9500000000L);
        require(state("cards").size() == 1, "not removed at exit start");
        accept(10100000000L);
        require(state("cards").isEmpty(), "removed only fully outside");
        darkgrey.rpg.client.TaskNotificationCards.clear();
        accept(0, event("a", "a", "received", ""));
        accept(
            100000000L,
            event("b", "b", "received", ""),
            event("c", "c", "received", ""),
            event("d", "d", "received", ""));
        require(state("cards").size() == 3 && state("pending").size() == 1, "fourth waits");
        Object second = state("cards").get("b");
        accept(5200000000L);
        require(state("cards").containsKey("d") && !state("cards").containsKey("a"), "queue handoff after full exit");
        require(
            number(second, "y", 5200000000L) == 100 && number(second, "y", 5500000000L) == 50,
            "smooth upward reflow");
        require(((Long) field(state("cards").get("d"), "entered")) == 5200000000L, "queued entry starts on admission");
        darkgrey.rpg.client.TaskNotificationCards.clear();
        accept(0, event("only", "t", "objective_completed", "独立目标"));
        Object only = state("cards").get("t");
        java.lang.reflect.Method body = only.getClass()
            .getDeclaredMethod("body");
        body.setAccessible(true);
        require(
            body.invoke(only)
                .equals("已完成：独立目标"),
            "completion-only body");
        accept(1, event("terminal", "t", "failed", ""), event("late", "t", "objective_active", "过时目标"));
        require(
            body.invoke(only)
                .equals("任务失败"),
            "terminal takes precedence over later objective");
        int previousOffset = 218;
        for (long tick = 0; tick <= 600000000L; tick += 10000000L) {
            int offset = darkgrey.rpg.client.TaskNotificationCards.slideOffset(218, tick);
            require(offset >= 0 && offset <= previousOffset, "monotonic eased entry");
            previousOffset = offset;
        }
        require(previousOffset == 0, "entry ends exactly at rest");
        List<String> wrapped = darkgrey.rpg.client.TaskNotificationCards
            .wrap("长任务名称😀还有更多文字\n下一行", 5, 2, new darkgrey.rpg.client.TaskNotificationCards.TextWidth() {

                public int width(String text) {
                    return text.codePointCount(0, text.length());
                }
            });
        require(
            wrapped.size() == 2 && wrapped.get(1)
                .endsWith("…"),
            "long text bounded with ellipsis");
        for (String line : wrapped) require(line.codePointCount(0, line.length()) <= 5, "line width limit");
        darkgrey.rpg.client.TaskNotificationCards.clear();
        accept(0, event("f", "t", "failed", ""));
        require(field(state("cards").get("t"), "terminal").equals("任务失败"), "failure result");
        darkgrey.rpg.client.TaskNotificationCards.clear();
    }

    private static CanonicalTaskJournalEntry task(CanonicalTaskInstanceStatus status,
        CanonicalTaskObjectiveStatus objective, int count) {
        return new CanonicalTaskJournalEntry(
            PLAYER,
            "story",
            "placement",
            "task",
            "Test",
            status,
            10,
            status == CanonicalTaskInstanceStatus.SETTLED ? Long.valueOf(20) : null,
            status == CanonicalTaskInstanceStatus.SETTLED ? "done" : null,
            Collections.<String, Boolean>emptyMap(),
            Collections.singletonList(
                new CanonicalTaskJournalObjectiveRow(
                    "objective",
                    "Collect",
                    "collect_item",
                    objective,
                    count,
                    2,
                    true,
                    "Collect")));
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
