package darkgrey.rpg.story.canonical.runtime;

import java.time.Clock;

/** Shared pure decision for real admission and administrator diagnostics. */
public final class CanonicalStoryRepeatEligibility {

    public final CanonicalStoryStartDisposition disposition;
    public final Long nextEligibleAt;
    public final String explanation;

    private CanonicalStoryRepeatEligibility(CanonicalStoryStartDisposition disposition, Long next, String explanation) {
        this.disposition = disposition;
        this.nextEligibleAt = next;
        this.explanation = explanation;
    }

    public static CanonicalStoryRepeatEligibility evaluate(CanonicalStoryStatus status,
        CanonicalStoryRepeatPolicy policy, Long completed, CanonicalStoryRepeatCondition condition, Clock clock) {
        if (status == CanonicalStoryStatus.ACTIVE)
            return new CanonicalStoryRepeatEligibility(CanonicalStoryStartDisposition.ALREADY_ACTIVE, null, "进行中");
        if (status != CanonicalStoryStatus.TERMINATED) return new CanonicalStoryRepeatEligibility(
            CanonicalStoryStartDisposition.ERROR_TERMINAL,
            null,
            "异常终止，不自动重试");
        if (policy == CanonicalStoryRepeatPolicy.ONCE)
            return new CanonicalStoryRepeatEligibility(CanonicalStoryStartDisposition.ONCE_TERMINAL, null, "仅一次");
        try {
            if (completed == null || completed.longValue() <= 0 || clock.millis() < completed.longValue())
                throw new IllegalArgumentException("正常完成时间缺失或服务器时钟倒退");
            long next = condition.nextEligibleAt(completed.longValue(), CanonicalStoryRepeatCondition.getServerZone());
            boolean eligible = clock.millis() >= next;
            return new CanonicalStoryRepeatEligibility(
                eligible ? CanonicalStoryStartDisposition.REPEATABLE_RESTART
                    : CanonicalStoryStartDisposition.REPEAT_WAITING,
                Long.valueOf(next),
                eligible ? "已具备资格，等待启动条件" : "cooldown".equals(condition.getType()) ? "冷却中" : "等待计划时刻");
        } catch (RuntimeException invalid) {
            return new CanonicalStoryRepeatEligibility(
                CanonicalStoryStartDisposition.INVALID_REPEAT_TIME,
                null,
                "时间记录或重复配置无效");
        }
    }
}
