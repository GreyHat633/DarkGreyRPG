package darkgrey.rpg.story.canonical.runtime;

import java.time.Clock;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.zone.ZoneOffsetTransition;
import java.util.Arrays;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Pure repeat eligibility arithmetic. No scheduler, mutations, or generated trigger events. */
public final class CanonicalStoryRepeatCondition {

    private static volatile ZoneId serverZone = ZoneId.systemDefault();
    private final String type;
    private final long duration;
    private final String period;
    private final int weekday;
    private final int month;
    private final int day;
    private final LocalTime time;

    private CanonicalStoryRepeatCondition(String type, long duration, String period, int weekday, int month, int day,
        LocalTime time) {
        this.type = type;
        this.duration = duration;
        this.period = period;
        this.weekday = weekday;
        this.month = month;
        this.day = day;
        this.time = time;
    }

    public static void configureServerZone(String id) {
        serverZone = ZoneId.of(id);
    }

    public static ZoneId getServerZone() {
        return serverZone;
    }

    public String getType() {
        return type;
    }

    public static CanonicalStoryRepeatCondition forResource(
        darkgrey.rpg.graph.canonical.CanonicalGraphResource resource) {
        for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
            .getNodes())
            if ("start".equals(node.getType())) return parse(
                node.getProperties()
                    .get("repeat_condition"));
        throw new IllegalArgumentException("Story Start is missing.");
    }

    public static CanonicalStoryRepeatCondition parse(JsonElement value) {
        if (value == null) return new CanonicalStoryRepeatCondition("none", 0, null, 0, 0, 0, null);
        try {
            JsonObject json = value.getAsJsonObject();
            String type = text(json, "type");
            if ("none".equals(type)) {
                keys(json, "type");
                return new CanonicalStoryRepeatCondition(type, 0, null, 0, 0, 0, null);
            }
            if ("cooldown".equals(type)) {
                keys(json, "type", "value", "unit");
                String unit = text(json, "unit");
                long factor = "seconds".equals(unit) ? 1000
                    : "minutes".equals(unit) ? 60000 : "hours".equals(unit) ? 3600000 : 0;
                long amount = integer(json, "value");
                if (factor == 0 || amount <= 0) throw new IllegalArgumentException("Invalid cooldown.");
                return new CanonicalStoryRepeatCondition(type, Math.multiplyExact(amount, factor), null, 0, 0, 0, null);
            }
            if (!"scheduled".equals(type)) throw new IllegalArgumentException("Unknown repeat type.");
            String period = text(json, "period");
            String timeText = text(json, "time");
            if (!timeText.matches("[0-9]{2}:[0-9]{2}")) throw new IllegalArgumentException("Expected HH:mm.");
            LocalTime time = LocalTime.parse(timeText);
            int weekday = 1, month = 1, day = 1;
            if ("daily".equals(period)) keys(json, "type", "period", "time");
            else if ("weekly".equals(period)) {
                keys(json, "type", "period", "time", "weekday");
                weekday = Math.toIntExact(integer(json, "weekday"));
                if (weekday < 1 || weekday > 7) throw new IllegalArgumentException("Invalid weekday.");
            } else if ("yearly".equals(period)) {
                keys(json, "type", "period", "time", "month", "day");
                month = Math.toIntExact(integer(json, "month"));
                day = Math.toIntExact(integer(json, "day"));
                LocalDate.of(2000, month, day);
            } else throw new IllegalArgumentException("Unknown repeat period.");
            return new CanonicalStoryRepeatCondition(type, 0, period, weekday, month, day, time);
        } catch (RuntimeException invalid) {
            throw new CanonicalGraphResourceException(
                "story.start.repeat_condition",
                "Invalid repeat condition: " + invalid.getMessage());
        }
    }

    public long nextEligibleAt(long completedAt, ZoneId zone) {
        if (completedAt <= 0) throw new IllegalArgumentException("Missing normal completion time.");
        if ("none".equals(type)) return completedAt;
        if ("cooldown".equals(type)) return Math.addExact(completedAt, duration);
        Instant completed = Instant.ofEpochMilli(completedAt);
        LocalDate date = completed.atZone(zone)
            .toLocalDate();
        // At most eight years includes the leap-century boundary (2096 -> 2104).
        for (int offset = 0; offset <= 366 * 8; offset++, date = date.plusDays(1)) {
            if ("weekly".equals(period) && date.getDayOfWeek()
                .getValue() != weekday) continue;
            if ("yearly".equals(period) && (date.getMonthValue() != month || date.getDayOfMonth() != day)) continue;
            LocalDateTime local = date.atTime(time);
            List<ZoneOffset> offsets = zone.getRules()
                .getValidOffsets(local);
            Instant scheduled;
            if (offsets.isEmpty()) {
                ZoneOffsetTransition transition = zone.getRules()
                    .getTransition(local);
                scheduled = transition.getInstant();
            } else scheduled = local.toInstant(offsets.get(0));
            if (scheduled.isAfter(completed)) return scheduled.toEpochMilli();
        }
        throw new IllegalArgumentException("No valid next schedule instant.");
    }

    public boolean isEligible(long completedAt, Clock clock, ZoneId zone) {
        return clock.millis() >= nextEligibleAt(completedAt, zone);
    }

    private static String text(JsonObject json, String name) {
        JsonElement value = json.get(name);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw new IllegalArgumentException("Expected string: " + name);
        return value.getAsString();
    }

    private static long integer(JsonObject json, String name) {
        JsonElement value = json.get(name);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw new IllegalArgumentException("Expected integer: " + name);
        return value.getAsBigDecimal()
            .longValueExact();
    }

    private static void keys(JsonObject json, String... names) {
        Set<String> actual = new HashSet<String>();
        for (Map.Entry<String, JsonElement> entry : json.entrySet()) actual.add(entry.getKey());
        if (!actual.equals(new HashSet<String>(Arrays.asList(names))))
            throw new IllegalArgumentException("Unknown or missing fields.");
    }
}
