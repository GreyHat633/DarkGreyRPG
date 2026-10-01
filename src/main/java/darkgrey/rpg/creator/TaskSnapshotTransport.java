package darkgrey.rpg.creator;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;

import io.netty.buffer.Unpooled;

/** Complete atomic snapshot in individually bounded existing envelopes. */
public final class TaskSnapshotTransport {

    public static final int MAX_BYTES = 32 * 1024 * 1024;
    public static final int CHUNK = 32768;

    private TaskSnapshotTransport() {}

    public static List<CreatorSnapshot> encode(NBTTagCompound data) {
        try {
            java.io.ByteArrayOutputStream raw = new java.io.ByteArrayOutputStream();
            CompressedStreamTools.write(data, new java.io.DataOutputStream(raw));
            if (raw.size() > MAX_BYTES)
                throw new IllegalArgumentException("Task presentation exceeds aggregate budget");
            byte[] bytes = raw.toByteArray();
            CompressedStreamTools.func_152456_a(
                new java.io.DataInputStream(new java.io.ByteArrayInputStream(bytes)),
                new net.minecraft.nbt.NBTSizeTracker(2L * MAX_BYTES));
            int count = (bytes.length + CHUNK - 1) / CHUNK;
            List<CreatorSnapshot> packets = new ArrayList<CreatorSnapshot>();
            for (int i = 0; i < count; i++) {
                NBTTagCompound part = new NBTTagCompound();
                part.setLong("revision", data.getLong("revision"));
                part.setInteger("dimension", data.getInteger("dimension"));
                part.setInteger("part", i);
                part.setInteger("parts", count);
                part.setInteger("total", bytes.length);
                part.setByteArray(
                    "payload",
                    java.util.Arrays.copyOfRange(bytes, i * CHUNK, Math.min(bytes.length, (i + 1) * CHUNK)));
                CreatorSnapshot packet = new CreatorSnapshot(1, part);
                io.netty.buffer.ByteBuf encoded = Unpooled.buffer();
                try {
                    packet.toBytes(encoded);
                } finally {
                    encoded.release();
                }
                packets.add(packet);
            }
            return packets;
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Cannot encode complete task snapshot", invalid);
        }
    }
}
