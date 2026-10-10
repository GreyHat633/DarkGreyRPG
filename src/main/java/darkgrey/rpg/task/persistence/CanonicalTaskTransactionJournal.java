package darkgrey.rpg.task.persistence;

import java.io.BufferedOutputStream;
import java.io.ByteArrayOutputStream;
import java.io.DataOutputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import java.util.zip.Deflater;
import java.util.zip.GZIPOutputStream;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTSizeTracker;
import net.minecraft.nbt.NBTTagCompound;

/**
 * One retained, atomically replaced after-image per player. A durable record is
 * the commit point: recovery applies that image, never the original delta.
 * The player image must carry cumulative receipts across subsequent commits.
 */
public final class CanonicalTaskTransactionJournal {

    private static final int MAX_BYTES = 8 * 1024 * 1024;
    private final Path directory;
    // Retain only a digest and receipt, never player images or world objects.
    private final Map<UUID, VerifiedRecord> verified = new LinkedHashMap<UUID, VerifiedRecord>(16, .75F, true) {

        @Override
        protected boolean removeEldestEntry(Map.Entry<UUID, VerifiedRecord> entry) {
            return size() > 256;
        }
    };

    public CanonicalTaskTransactionJournal(Path directory) {
        if (directory == null) throw new IllegalArgumentException("Journal directory required.");
        this.directory = directory.toAbsolutePath()
            .normalize();
    }

    public interface PlayerState {

        boolean hasReceipt(String receipt);

        /** Apply the entire validated image, including its persistent receipt. */
        void apply(NBTTagCompound image);

        /** Persist and verify the player checkpoint, or throw. */
        void checkpoint() throws IOException;
    }

    public synchronized boolean recover(UUID player, PlayerState state) throws IOException {
        NBTTagCompound record = read(player);
        if (record == null || state.hasReceipt(record.getString("receipt"))) return false;
        state.apply(copy(record.getCompoundTag("image")));
        if (!state.hasReceipt(record.getString("receipt")))
            throw new IOException("Recovered image omitted its receipt.");
        state.checkpoint();
        return true;
    }

    /**
     * Call recover before preparing an image. Exceptions after persist must not
     * trigger rollback: the durable decision is recovered forward next login.
     */
    public synchronized boolean commit(UUID player, String receipt, NBTTagCompound image, PlayerState state)
        throws IOException {
        requireReceipt(receipt);
        if (player == null || state == null || image == null)
            throw new IllegalArgumentException("Player, image and state required.");
        if (state.hasReceipt(receipt)) return false;
        if (!image.getCompoundTag("receipts")
            .getBoolean(receipt)) throw new IOException("Task after-image must contain its receipt before commit.");
        requireRecoveredPrevious(player, state);
        NBTTagCompound record = new NBTTagCompound();
        record.setInteger("schema_version", 1);
        record.setString("player_uuid", player.toString());
        record.setString("receipt", receipt);
        record.setTag("image", copy(image));
        persist(player, record);
        state.apply(copy(image));
        if (!state.hasReceipt(receipt)) throw new IOException("Applied Task image omitted its receipt.");
        state.checkpoint();
        return true;
    }

    private void persist(UUID player, NBTTagCompound record) throws IOException {
        byte[] bytes = compressJournal(record);
        if (bytes.length > MAX_BYTES) throw new IOException("Task transaction exceeds journal limit.");
        Files.createDirectories(directory);
        Path temporary = directory.resolve(player.toString() + ".nbt.tmp");
        try (FileOutputStream output = new FileOutputStream(temporary.toFile())) {
            output.write(bytes);
            output.flush();
            output.getFD()
                .sync();
        }
        // No delete-then-rename fallback: unsupported atomic replacement fails
        // before player mutation and leaves the previous journal recoverable.
        Files.move(temporary, path(player), StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        verified.put(player, new VerifiedRecord(digest(bytes), record.getString("receipt")));
    }

    /** Same gzip NBT format; favor server-thread latency over compression ratio. */
    private static byte[] compressJournal(NBTTagCompound record) throws IOException {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        try (GZIPOutputStream gzip = new GZIPOutputStream(bytes, 8192) {

            {
                def.setLevel(Deflater.BEST_SPEED);
            }
        }; DataOutputStream output = new DataOutputStream(new BufferedOutputStream(gzip, 32768))) {
            CompressedStreamTools.write(record, output);
        }
        byte[] result = bytes.toByteArray();
        // Keep the previous compressed-size admission boundary for unusually large
        // records: a faster but larger representation must not reject an old valid image.
        return result.length <= MAX_BYTES ? result : CompressedStreamTools.compress(record);
    }

    private NBTTagCompound read(UUID player) throws IOException {
        byte[] bytes = readBytes(player);
        return bytes == null ? null : decode(player, bytes);
    }

    private void requireRecoveredPrevious(UUID player, PlayerState state) throws IOException {
        byte[] bytes = readBytes(player);
        if (bytes == null) return;
        VerifiedRecord cached = verified.get(player);
        String receipt = cached != null && MessageDigest.isEqual(cached.digest, digest(bytes)) ? cached.receipt
            : decode(player, bytes).getString("receipt");
        if (!state.hasReceipt(receipt))
            throw new IOException("Unrecovered Task transaction; refusing to overwrite its recovery record.");
    }

    private byte[] readBytes(UUID player) throws IOException {
        if (player == null) throw new IllegalArgumentException("Player required.");
        Path file = path(player);
        if (!Files.exists(file)) {
            verified.remove(player);
            return null;
        }
        if (Files.size(file) > MAX_BYTES) throw new IOException("Task transaction exceeds journal limit.");
        byte[] bytes = Files.readAllBytes(file);
        if (bytes.length > MAX_BYTES) throw new IOException("Task transaction exceeds journal limit.");
        return bytes;
    }

    private NBTTagCompound decode(UUID player, byte[] bytes) throws IOException {
        NBTTagCompound record;
        try {
            record = CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(MAX_BYTES));
            if (record.func_150296_c()
                .size() != 4 || !record.hasKey("schema_version", 3)
                || record.getInteger("schema_version") != 1
                || !record.hasKey("player_uuid", 8)
                || !player.toString()
                    .equals(record.getString("player_uuid"))
                || !record.hasKey("receipt", 8)
                || !record.hasKey("image", 10)) throw new IOException("Invalid Task transaction record.");
            requireReceipt(record.getString("receipt"));
        } catch (RuntimeException invalid) {
            throw new IOException("Unreadable Task transaction; recovery required.", invalid);
        }
        verified.put(player, new VerifiedRecord(digest(bytes), record.getString("receipt")));
        return record;
    }

    private static byte[] digest(byte[] bytes) {
        try {
            return MessageDigest.getInstance("SHA-256")
                .digest(bytes);
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException(impossible);
        }
    }

    private static final class VerifiedRecord {

        final byte[] digest;
        final String receipt;

        VerifiedRecord(byte[] digest, String receipt) {
            this.digest = digest;
            this.receipt = receipt;
        }
    }

    private Path path(UUID player) {
        return directory.resolve(player.toString() + ".nbt");
    }

    private static void requireReceipt(String receipt) {
        if (!TaskReceiptKey.isValid(receipt))
            throw new IllegalArgumentException("Task receipt must be an internal SHA-256 key.");
    }

    private static NBTTagCompound copy(NBTTagCompound value) {
        return (NBTTagCompound) value.copy();
    }
}
