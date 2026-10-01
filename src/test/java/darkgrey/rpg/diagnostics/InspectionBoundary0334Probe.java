package darkgrey.rpg.diagnostics;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Real disk / wire boundary checks against the same production readers. */
public final class InspectionBoundary0334Probe {

    public static void main(String[] args) throws Exception {
        Path folder = Files.createTempDirectory("inspection-boundaries-");
        Path file = folder.resolve("fixture.dat");
        try {
            if (ReadOnlyStateSource.readFile(file.toFile()) != null || Files.exists(file))
                throw new AssertionError("Missing source created");
            NBTTagCompound root = new NBTTagCompound();
            NBTTagCompound data = new NBTTagCompound();
            data.setString("marker", "unchanged");
            root.setTag("data", data);
            byte[] valid = CompressedStreamTools.compress(root);
            Files.write(file, valid);
            NBTTagCompound loaded = ReadOnlyStateSource.readFile(file.toFile());
            if (!"unchanged".equals(loaded.getString("marker"))) throw new AssertionError("Disk projection lost data");
            loaded.setString("marker", "detached");
            if (!Arrays.equals(valid, Files.readAllBytes(file))) throw new AssertionError("Read wrote disk");
            rejectFile(file, new byte[] { 1, 2, 3 });
            rejectFile(file, Arrays.copyOf(valid, 8));
            rejectFile(file, CompressedStreamTools.compress(new NBTTagCompound()));
            root.setString("data", "wrong type");
            rejectFile(file, CompressedStreamTools.compress(root));
            try (java.io.RandomAccessFile oversized = new java.io.RandomAccessFile(file.toFile(), "rw")) {
                oversized.setLength(16777217);
            }
            boolean rejected = false;
            try {
                ReadOnlyStateSource.readFile(file.toFile());
            } catch (java.io.IOException expected) {
                rejected = true;
            }
            if (!rejected || Files.size(file) != 16777217) throw new AssertionError("Disk cap / readonly failure");

            rejectPacket(new byte[12]);
            rejectPacket(new byte[262158]);
            for (int kind : new int[] { 3, 255 }) {
                ByteBuf bad = Unpooled.buffer()
                    .writeByte(kind)
                    .writeLong(7)
                    .writeInt(1)
                    .writeByte(0);
                rejectPacket(bytes(bad));
            }
            rejectPacket(
                bytes(
                    Unpooled.buffer()
                        .writeByte(1)
                        .writeLong(7)
                        .writeInt(2)
                        .writeByte(0)));
            rejectPacket(
                bytes(
                    Unpooled.buffer()
                        .writeByte(1)
                        .writeLong(7)
                        .writeInt(257)
                        .writeZero(257)));
            rejectPacket(
                bytes(
                    Unpooled.buffer()
                        .writeByte(2)
                        .writeLong(7)
                        .writeInt(1)
                        .writeByte(0)));
            NBTTagCompound bomb = new NBTTagCompound();
            bomb.setByteArray("large", new byte[1100000]);
            ByteBuf encoded = Unpooled.buffer();
            new PlayerStatePacket(2, 1, bomb).toBytes(encoded);
            rejectPacket(bytes(encoded));
            if ("1".equals(System.getenv("DGR_INSPECTION_SCALE"))) diskScale(file);
            System.out.println(
                "InspectionBoundary0334Probe PASS: real disk missing/corrupt/truncated/wrong root/oversize readonly; wire short/oversize/kind/length/corrupt/decompressed cap");
        } finally {
            Files.deleteIfExists(file);
            Files.deleteIfExists(folder);
        }
    }

    private static void diskScale(Path file) throws Exception {
        java.util.UUID player = new java.util.UUID(1, 2);
        net.minecraft.nbt.NBTTagList records = new net.minecraft.nbt.NBTTagList();
        for (int i = 0; i < 10000; i++) {
            NBTTagCompound record = new NBTTagCompound();
            record.setString("player_uuid", (i < 1000 ? player : new java.util.UUID(0, i + 1)).toString());
            record.setString("task_resource_id", "scale:task");
            record.setString("task_node_placement_id", "placement-" + i);
            record.setString("status", "ACTIVE");
            records.appendTag(record);
        }
        NBTTagCompound data = new NBTTagCompound();
        data.setTag("instances", records);
        NBTTagCompound root = new NBTTagCompound();
        root.setTag("data", data);
        byte[] original = CompressedStreamTools.compress(root);
        Files.write(file, original);
        double[] samples = new double[30];
        for (int i = 0; i < 35; i++) {
            long started = System.nanoTime();
            NBTTagCompound disk = ReadOnlyStateSource.readFile(file.toFile());
            java.util.List<NBTTagCompound> rows = new java.util.ArrayList<NBTTagCompound>();
            PlayerStateInspection
                .collect(rows, "Task", disk, player, "disk", darkgrey.rpg.project.ProjectSnapshot.empty());
            if (rows.size() != 1000) throw new AssertionError("Disk projection count");
            NBTTagCompound page = new NBTTagCompound();
            net.minecraft.nbt.NBTTagList pageRows = new net.minecraft.nbt.NBTTagList();
            for (int n = 0; n < PlayerStateInspection.PAGE_SIZE; n++) pageRows.appendTag(rows.get(n));
            page.setTag("rows", pageRows);
            page.setInteger("total", rows.size());
            ByteBuf wire = Unpooled.buffer();
            try {
                new PlayerStatePacket(2, i, page).toBytes(wire);
                PlayerStatePacket received = new PlayerStatePacket();
                received.fromBytes(wire);
                if (received.request != i || received.data.getTagList("rows", 10)
                    .tagCount() != 16) throw new AssertionError("Disk wire page mismatch");
            } finally {
                wire.release();
            }
            if (i >= 5) samples[i - 5] = (System.nanoTime() - started) / 1000000.0;
        }
        if (!Arrays.equals(original, Files.readAllBytes(file))) throw new AssertionError("Disk scale mutated source");
        Arrays.sort(samples);
        System.out.println(
            "INSPECTION_DISK_WIRE_SCALE records=10000 target=1000 page=16 n=30 disk_bytes=" + original.length
                + " median_ms="
                + samples[15]
                + " p95_ms="
                + samples[28]
                + " max_ms="
                + samples[29]
                + " includes disk read/decompress/projection/page compression/decode; excludes socket and GUI");
    }

    private static byte[] bytes(ByteBuf buffer) {
        try {
            byte[] result = new byte[buffer.readableBytes()];
            buffer.readBytes(result);
            return result;
        } finally {
            buffer.release();
        }
    }

    private static void rejectFile(Path file, byte[] content) throws Exception {
        Files.write(file, content);
        boolean rejected = false;
        try {
            ReadOnlyStateSource.readFile(file.toFile());
        } catch (Exception expected) {
            rejected = true;
        }
        if (!rejected || !Arrays.equals(content, Files.readAllBytes(file)))
            throw new AssertionError("Invalid disk source accepted or changed");
    }

    private static void rejectPacket(byte[] content) {
        ByteBuf input = Unpooled.wrappedBuffer(content);
        boolean rejected = false;
        try {
            new PlayerStatePacket().fromBytes(input);
        } catch (RuntimeException expected) {
            rejected = true;
        } finally {
            input.release();
        }
        if (!rejected) throw new AssertionError("Malformed packet accepted");
    }
}
