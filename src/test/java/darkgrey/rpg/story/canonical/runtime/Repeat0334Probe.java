package darkgrey.rpg.story.canonical.runtime;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;

import com.google.gson.JsonParser;

public final class Repeat0334Probe {

    public static void main(String[] args) {
        check(
            "{\"type\":\"cooldown\",\"value\":24,\"unit\":\"hours\"}",
            "2026-09-24T13:30:00Z",
            "UTC",
            "2026-09-25T13:30:00Z");
        check(
            "{\"type\":\"scheduled\",\"period\":\"daily\",\"time\":\"12:00\"}",
            "2026-09-24T12:00:00Z",
            "UTC",
            "2026-09-25T12:00:00Z");
        check(
            "{\"type\":\"scheduled\",\"period\":\"weekly\",\"weekday\":1,\"time\":\"12:00\"}",
            "2026-09-24T12:00:00Z",
            "UTC",
            "2026-09-28T12:00:00Z");
        check(
            "{\"type\":\"scheduled\",\"period\":\"yearly\",\"month\":2,\"day\":29,\"time\":\"12:00\"}",
            "2096-02-29T12:00:00Z",
            "UTC",
            "2104-02-29T12:00:00Z");
        check(
            "{\"type\":\"scheduled\",\"period\":\"daily\",\"time\":\"02:30\"}",
            "2026-03-08T05:00:00Z",
            "America/New_York",
            "2026-03-08T07:00:00Z");
        check(
            "{\"type\":\"scheduled\",\"period\":\"daily\",\"time\":\"01:30\"}",
            "2026-11-01T05:30:00Z",
            "America/New_York",
            "2026-11-02T06:30:00Z");
        invalid("{\"type\":\"cooldown\",\"value\":0,\"unit\":\"hours\"}");
        invalid("{\"type\":\"cooldown\",\"value\":9223372036854775807,\"unit\":\"hours\"}");
        invalid("{\"type\":\"cooldown\",\"value\":1.5,\"unit\":\"hours\"}");
        invalid("{\"type\":\"scheduled\",\"period\":\"monthly\",\"time\":\"12:00\"}");
        invalid("{\"type\":\"scheduled\",\"period\":\"yearly\",\"month\":2,\"day\":30,\"time\":\"12:00\"}");
        System.out.println(
            "Repeat0334Probe PASS: cooldown, strict boundary, weekly, leap century, DST gap/overlap, persistent eligibility, invalid rules");
    }

    private static void check(String json, String completed, String zone, String expected) {
        CanonicalStoryRepeatCondition condition = CanonicalStoryRepeatCondition.parse(new JsonParser().parse(json));
        long completion = Instant.parse(completed)
            .toEpochMilli();
        long next = Instant.parse(expected)
            .toEpochMilli();
        if (condition.nextEligibleAt(completion, ZoneId.of(zone)) != next) throw new AssertionError(json);
        if (condition
            .isEligible(completion, Clock.fixed(Instant.ofEpochMilli(next - 1), ZoneOffset.UTC), ZoneId.of(zone)))
            throw new AssertionError("early");
        if (!condition.isEligible(completion, Clock.fixed(Instant.ofEpochMilli(next), ZoneOffset.UTC), ZoneId.of(zone)))
            throw new AssertionError("boundary");
        if (!condition.isEligible(
            completion,
            Clock.fixed(Instant.ofEpochMilli(next + 86400000L), ZoneOffset.UTC),
            ZoneId.of(zone))) throw new AssertionError("missed eligibility");
    }

    private static void invalid(String json) {
        try {
            CanonicalStoryRepeatCondition.parse(new JsonParser().parse(json));
        } catch (RuntimeException expected) {
            return;
        }
        throw new AssertionError("Accepted invalid rule: " + json);
    }
}
