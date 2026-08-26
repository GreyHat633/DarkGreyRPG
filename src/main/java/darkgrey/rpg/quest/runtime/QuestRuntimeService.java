package darkgrey.rpg.quest.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.S2CQuestJournal;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.CollectItemObjective;
import darkgrey.rpg.quest.InteractActorObjective;
import darkgrey.rpg.quest.KillEntityObjective;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;
import darkgrey.rpg.quest.ReachLocationObjective;
import darkgrey.rpg.runtime.ChatMessages;

public final class QuestRuntimeService {

    private final ProjectRepository repository;
    private final List<QuestCompletionListener> completionListeners = new ArrayList<QuestCompletionListener>();

    public QuestRuntimeService(ProjectRepository repository) {
        this.repository = repository;
    }

    public boolean start(EntityPlayerMP player, String questId) {
        QuestDefinition quest = repository.getSnapshot()
            .getQuest(questId);
        if (quest == null) {
            ChatMessages.error(player, "Unknown Quest ID: " + questId);
            return false;
        }
        PlayerQuestData data = PlayerQuestData.get(player);
        QuestProgressRecord existing = data.getQuest(player, questId);
        if (existing != null) {
            ChatMessages
                .info(player, "Quest already exists in journal: " + questId + " (" + existing.getStatus() + ")");
            return false;
        }
        data.startQuest(player, questId);
        ChatMessages.success(player, "Quest started: " + quest.getTitle());
        return true;
    }

    public boolean reset(EntityPlayerMP player, String questId) {
        boolean removed = PlayerQuestData.get(player)
            .resetQuest(player, questId);
        if (removed) {
            ChatMessages.success(player, "Quest state reset: " + questId);
        } else {
            ChatMessages.info(player, "Quest was not present in journal: " + questId);
        }
        return removed;
    }

    public void addCompletionListener(QuestCompletionListener listener) {
        completionListeners.add(listener);
    }

    public QuestStatus getStatus(EntityPlayerMP player, String questId) {
        QuestProgressRecord record = PlayerQuestData.get(player)
            .getQuest(player, questId);
        return record == null ? null : record.getStatus();
    }

    public boolean complete(EntityPlayerMP player, String questId) {
        QuestDefinition quest = repository.getSnapshot()
            .getQuest(questId);
        if (quest == null) {
            return false;
        }
        PlayerQuestData data = PlayerQuestData.get(player);
        QuestProgressRecord record = data.getQuest(player, questId);
        if (record == null) {
            record = data.startQuest(player, questId);
        }
        if (record.getStatus() != QuestStatus.COMPLETED) {
            for (QuestObjective objective : quest.getObjectives()) {
                record.setProgress(objective.getId(), objective.getRequiredAmount());
            }
            record.setStatus(QuestStatus.COMPLETED);
            data.markDirty();
            notifyCompleted(player, questId);
        }
        return true;
    }

    public void accept(EntityPlayerMP player, QuestEvent event) {
        PlayerQuestData data = PlayerQuestData.get(player);
        List<QuestProgressRecord> records = new ArrayList<QuestProgressRecord>(data.getQuests(player));
        for (QuestProgressRecord record : records) {
            if (record.getStatus() != QuestStatus.ACTIVE) {
                continue;
            }
            QuestDefinition quest = repository.getSnapshot()
                .getQuest(record.getQuestId());
            if (quest == null) {
                continue;
            }
            QuestStatus previousStatus = record.getStatus();
            for (QuestObjective objective : quest.getObjectives()) {
                if (matches(objective, event)
                    && QuestProgressEvaluator.addProgress(quest, record, objective.getId(), event.getAmount())) {
                    data.markDirty();
                    ChatMessages.info(
                        player,
                        quest.getTitle() + ": "
                            + objective.getDescription()
                            + " "
                            + record.getProgress(objective.getId())
                            + "/"
                            + objective.getRequiredAmount());
                }
            }
            if (previousStatus != QuestStatus.COMPLETED && record.getStatus() == QuestStatus.COMPLETED) {
                data.markDirty();
                ChatMessages.success(player, "Quest completed: " + quest.getTitle());
                notifyCompleted(player, quest.getId());
            }
        }
    }

    public List<QuestJournalEntry> getJournal(EntityPlayerMP player) {
        List<QuestJournalEntry> entries = new ArrayList<QuestJournalEntry>();
        for (QuestProgressRecord record : PlayerQuestData.get(player)
            .getQuests(player)) {
            QuestDefinition quest = repository.getSnapshot()
                .getQuest(record.getQuestId());
            if (quest == null) {
                continue;
            }
            List<String> lines = new ArrayList<String>();
            for (QuestObjective objective : quest.getObjectives()) {
                lines.add(
                    objective.getDescription() + "  "
                        + Math.min(record.getProgress(objective.getId()), objective.getRequiredAmount())
                        + "/"
                        + objective.getRequiredAmount());
            }
            entries.add(
                new QuestJournalEntry(
                    quest.getId(),
                    quest.getTitle(),
                    quest.getDescription(),
                    record.getStatus(),
                    lines));
        }
        Collections.sort(entries, new Comparator<QuestJournalEntry>() {

            @Override
            public int compare(QuestJournalEntry left, QuestJournalEntry right) {
                int status = left.getStatus()
                    .compareTo(right.getStatus());
                return status != 0 ? status
                    : left.getTitle()
                        .compareToIgnoreCase(right.getTitle());
            }
        });
        return entries;
    }

    public void openJournal(EntityPlayerMP player) {
        DialogueNetwork.CHANNEL.sendTo(new S2CQuestJournal(getJournal(player)), player);
    }

    private void notifyCompleted(EntityPlayerMP player, String questId) {
        for (QuestCompletionListener listener : completionListeners) {
            listener.onQuestCompleted(player, questId);
        }
    }

    private static boolean matches(QuestObjective objective, QuestEvent event) {
        if (objective.getType() != event.getType()) {
            return false;
        }
        if (objective instanceof KillEntityObjective) {
            return idMatches(((KillEntityObjective) objective).getEntityId(), event.getTargetId());
        }
        if (objective instanceof CollectItemObjective) {
            CollectItemObjective collect = (CollectItemObjective) objective;
            return idMatches(collect.getItemId(), event.getTargetId())
                && (collect.getMetadata() < 0 || collect.getMetadata() == event.getMetadata());
        }
        if (objective instanceof InteractActorObjective) {
            return ((InteractActorObjective) objective).getActorId()
                .equals(event.getTargetId());
        }
        if (objective instanceof ReachLocationObjective) {
            ReachLocationObjective reach = (ReachLocationObjective) objective;
            if (reach.getDimension() != event.getDimension()) {
                return false;
            }
            double deltaX = reach.getX() - event.getX();
            double deltaY = reach.getY() - event.getY();
            double deltaZ = reach.getZ() - event.getZ();
            return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= reach.getRadius() * reach.getRadius();
        }
        return false;
    }

    private static boolean idMatches(String configured, String actual) {
        if (configured.equalsIgnoreCase(actual)) {
            return true;
        }
        int separator = configured.indexOf(':');
        return separator >= 0 && configured.substring(separator + 1)
            .equalsIgnoreCase(actual);
    }
}
