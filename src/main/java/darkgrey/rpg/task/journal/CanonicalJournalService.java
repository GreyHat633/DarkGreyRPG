package darkgrey.rpg.task.journal;

import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.S2CQuestJournal;
import darkgrey.rpg.quest.runtime.CanonicalTaskLegacyJournalAdapter;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;

/**
 * Canonical owner for the Quest Journal transport boundary.
 *
 * <p>
 * The client packet remains a compatibility DTO, but its contents are
 * projected only from canonical Task instances.
 * </p>
 */
public final class CanonicalJournalService {

    private final CanonicalTaskForgeManager canonicalTaskManager;

    public CanonicalJournalService(CanonicalTaskForgeManager canonicalTaskManager) {
        if (canonicalTaskManager == null) throw new IllegalArgumentException("Canonical Task manager is required.");
        this.canonicalTaskManager = canonicalTaskManager;
    }

    /** Returns the canonical Task journal through the existing wire DTO adapter. */
    public List<QuestJournalEntry> getJournal(EntityPlayerMP player) {
        List<QuestJournalEntry> result = new java.util.ArrayList<QuestJournalEntry>();
        for (CanonicalTaskJournalEntry entry : canonicalTaskManager.getJournal(player))
            result.add(CanonicalTaskLegacyJournalAdapter.adapt(entry,
                text -> darkgrey.rpg.session.forge.DynamicContentResolver.resolve(text, player)));
        return java.util.Collections.unmodifiableList(result);
    }

    public List<QuestJournalEntry> journal(EntityPlayerMP player) {
        return getJournal(player);
    }

    /** Sends the canonical projection using the existing Quest Journal packet. */
    public void openJournal(EntityPlayerMP player) {
        if (player == null) throw new IllegalArgumentException("Journal player is required.");
        DialogueNetwork.CHANNEL.sendTo(new S2CQuestJournal(getJournal(player)), player);
    }
}
