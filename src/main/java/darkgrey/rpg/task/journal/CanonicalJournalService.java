package darkgrey.rpg.task.journal;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.function.UnaryOperator;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalTaskViewOpen;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Read-only command orchestration over the current Task journal and presentation. */
public final class CanonicalJournalService {

    private final CanonicalTaskForgeManager canonicalTaskManager;

    public CanonicalJournalService(CanonicalTaskForgeManager canonicalTaskManager) {
        if (canonicalTaskManager == null) throw new IllegalArgumentException("Canonical Task manager is required.");
        this.canonicalTaskManager = canonicalTaskManager;
    }

    public List<CanonicalTaskJournalEntry> getJournal(EntityPlayerMP player) {
        return canonicalTaskManager.getJournal(player);
    }

    public void openJournal(EntityPlayerMP player) {
        if (player == null) throw new IllegalArgumentException("Journal player is required.");
        darkgrey.rpg.creator.CanonicalTaskPresentationServer.push(player, true);
        DialogueNetwork.CHANNEL.sendTo(new CanonicalTaskViewOpen(player.dimension), player);
    }

    /** Current command text; the task menu continues to use its existing structured projection. */
    public static List<String> objectiveLines(CanonicalTaskJournalEntry entry, UnaryOperator<String> resolveText) {
        if (entry == null || resolveText == null)
            throw new IllegalArgumentException("Journal text context is required.");
        List<String> lines = new ArrayList<String>();
        for (CanonicalTaskJournalObjectiveRow row : entry.getObjectiveRows()) {
            if (row.getRuntimeStatus() != CanonicalTaskObjectiveStatus.ACTIVE) continue;
            String line = row.getDisplayLine();
            if (row.getDescription()
                .startsWith(darkgrey.rpg.session.runtime.DynamicContentText.PREFIX))
                line = line.replace(row.getDescription(), resolveText.apply(row.getDescription()));
            StringBuilder safe = new StringBuilder();
            for (char value : line.toCharArray()) safe.append(value < 0x20 || value == 0x7f ? '?' : value);
            lines.add(safe.toString());
        }
        return Collections.unmodifiableList(lines);
    }
}
