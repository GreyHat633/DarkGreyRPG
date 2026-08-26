package darkgrey.rpg.quest.runtime;

import darkgrey.rpg.quest.ObjectiveGroup;
import darkgrey.rpg.quest.ObjectiveGroupMode;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;

public final class QuestProgressEvaluator {

    private QuestProgressEvaluator() {}

    public static boolean canProgress(QuestDefinition quest, QuestProgressRecord record, String objectiveId) {
        if (record.getStatus() != QuestStatus.ACTIVE) {
            return false;
        }
        QuestObjective objective = quest.getObjective(objectiveId);
        if (objective == null || isObjectiveComplete(objective, record)) {
            return false;
        }
        for (ObjectiveGroup group : quest.getObjectiveGroups()) {
            if (!group.getObjectiveIds()
                .contains(objectiveId)) {
                continue;
            }
            if (group.getMode() != ObjectiveGroupMode.SEQUENCE) {
                return true;
            }
            for (String memberId : group.getObjectiveIds()) {
                QuestObjective member = quest.getObjective(memberId);
                if (!isObjectiveComplete(member, record)) {
                    return memberId.equals(objectiveId);
                }
            }
        }
        return false;
    }

    public static boolean addProgress(QuestDefinition quest, QuestProgressRecord record, String objectiveId,
        int amount) {
        if (amount <= 0 || !canProgress(quest, record, objectiveId)) {
            return false;
        }
        QuestObjective objective = quest.getObjective(objectiveId);
        int updated = Math.min(objective.getRequiredAmount(), record.getProgress(objectiveId) + amount);
        record.setProgress(objectiveId, updated);
        if (isQuestComplete(quest, record)) {
            record.setStatus(QuestStatus.COMPLETED);
        }
        return true;
    }

    public static boolean isQuestComplete(QuestDefinition quest, QuestProgressRecord record) {
        for (ObjectiveGroup group : quest.getObjectiveGroups()) {
            if (!isGroupComplete(quest, record, group)) {
                return false;
            }
        }
        return !quest.getObjectiveGroups()
            .isEmpty();
    }

    private static boolean isGroupComplete(QuestDefinition quest, QuestProgressRecord record, ObjectiveGroup group) {
        if (group.getMode() == ObjectiveGroupMode.ANY) {
            for (String objectiveId : group.getObjectiveIds()) {
                if (isObjectiveComplete(quest.getObjective(objectiveId), record)) {
                    return true;
                }
            }
            return false;
        }
        for (String objectiveId : group.getObjectiveIds()) {
            if (!isObjectiveComplete(quest.getObjective(objectiveId), record)) {
                return false;
            }
        }
        return true;
    }

    private static boolean isObjectiveComplete(QuestObjective objective, QuestProgressRecord record) {
        return objective != null && record.getProgress(objective.getId()) >= objective.getRequiredAmount();
    }
}
