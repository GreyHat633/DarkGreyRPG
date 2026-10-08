package darkgrey.rpg.nominator;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Collections;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.persistence.StorageProbeSupport;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;

/** Historical pre-fix reproduction. Deliberately excluded from the final verification suite. */
public final class StorageBaseline0401Probe {

    private StorageBaseline0401Probe() {}

    public static void main(String[] args) throws Exception {
        Path root = StorageProbeSupport.root("baseline-");
        for (int count : new int[] { 512, 513 }) {
            Path directory = root.resolve("bindings-" + count);
            MapStorage storage = StorageProbeSupport.storage(directory);
            NominatorSavedData data = NominatorSavedData.get(storage);
            for (int i = 0; i < count; i++) data.put(
                new NominatorEntityBinding(
                    new UUID(0, i + 1),
                    null,
                    Collections.singletonList(Nominator0400Probe.BANDITS),
                    Nominator0400Probe.STORY));
            storage.saveAllData();
            boolean readable = true;
            try {
                NominatorSavedData.get(StorageProbeSupport.storage(directory))
                    .bindings();
            } catch (IllegalStateException expected) {
                readable = false;
            }
            StorageProbeSupport.require(readable == (count == 512), "Expected historical capacity mismatch.");
            System.out.println("BASELINE_R1 count=" + count + " written=true readable=" + readable);
        }
        for (String vector : new String[] { "gzip", "schema", "line-context" }) {
            Path directory = root.resolve(vector);
            MapStorage storage = StorageProbeSupport.storage(directory);
            Path file = directory.resolve(CanonicalSessionSavedData.DATA_NAME + ".dat");
            if ("gzip".equals(vector)) Files.write(file, new byte[] { 1, 2, 3, 4 });
            else {
                CanonicalSessionSavedData fresh = new CanonicalSessionSavedData();
                NBTTagCompound payload = new NBTTagCompound();
                fresh.writeToNBT(payload);
                if ("schema".equals(vector)) payload.setInteger("schema_version", -1);
                else {
                    NBTTagCompound contexts = new NBTTagCompound();
                    contexts.setByteArray("1", new byte[] { 1, 2, 3 });
                    payload.setTag("line_contexts", contexts);
                }
                StorageProbeSupport.write(file, payload);
            }
            String before = StorageProbeSupport.hash(file);
            CanonicalSessionSavedData cached = CanonicalSessionSavedData.get(storage);
            StorageProbeSupport.require(cached.size() == 0, "Historical failure exposes an empty bound object.");
            StorageProbeSupport.forceDirty(cached);
            storage.saveAllData();
            boolean changed = !before.equals(StorageProbeSupport.hash(file));
            StorageProbeSupport.require(changed, "Historical forced-dirty save overwrites damaged file.");
            System.out
                .println("BASELINE_R2 vector=" + vector + " cached_empty_success=true forced_dirty_rewrite=" + changed);
        }
        System.out.println("BASELINE_STORAGE_FIXTURES=" + root);
    }
}
