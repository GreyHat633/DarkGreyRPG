package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.HashSet;
import java.util.Properties;
import java.util.Set;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Serialized on the gramophone IO worker. Manifest is the durable configuration commit record. */
public final class GramophoneBlobStore {

    public static final long MAX_STORE_BYTES = 512L * 1024 * 1024;
    private final Path root;
    private final Properties records = new Properties();

    public GramophoneBlobStore(Path root) throws IOException {
        this.root = root;
        GramophoneFiles.directory(root);
        Path manifest = root.resolve("Devices.properties");
        GramophoneFiles.check(manifest);
        if (!Files.exists(manifest))
            try (java.nio.file.DirectoryStream<Path> existing = Files.newDirectoryStream(root, "*.dgrmp3")) {
                if (existing.iterator()
                    .hasNext()) throw new IOException("媒体存在但引用索引缺失，请恢复对应存档的 Devices.properties");
            }
        if (Files.exists(manifest)) try (java.io.InputStream input = Files.newInputStream(manifest)) {
            if (Files.size(manifest) > 8 * 1024 * 1024) throw new IOException("留声机引用索引过大");
            records.load(input);
        }
        // Fail closed on a damaged manifest: never infer unloaded owners from loaded chunks.
        for (String key : records.stringPropertyNames())
            if (key.startsWith("device.")) decode(records.getProperty(key));
        if (!Files.exists(manifest)) flush();
        Path incoming = root.resolve("Incoming");
        if (Files.exists(incoming)) {
            GramophoneFiles.check(incoming);
            try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(incoming, "upload-*.part")) {
                for (Path file : files) if (System.currentTimeMillis() - Files.getLastModifiedTime(file)
                    .toMillis() > 86400000L) GramophoneFiles.retire(file);
            }
        }
        collect(new HashSet<String>());
    }

    public Path blob(String hash) throws IOException {
        if (!hash.matches("[0-9a-f]{64}")) throw new IOException("无效音频指纹");
        Path path = root.resolve(hash + ".dgrmp3");
        GramophoneFiles.check(path);
        return path;
    }

    public Path temporary() throws IOException {
        GramophoneFiles.directory(root.resolve("Incoming"));
        return Files.createTempFile(root.resolve("Incoming"), "upload-", ".part");
    }

    public GramophonePacket saved(String owner) throws IOException {
        String value = records.getProperty("device." + owner);
        return value == null ? null : decode(value);
    }

    public void commit(GramophonePacket config) throws IOException {
        String key = "device." + config.key();
        String old = records.getProperty(key);
        records.setProperty(key, encode(config));
        try {
            flush();
        } catch (IOException exception) {
            if (old == null) records.remove(key);
            else records.setProperty(key, old);
            throw exception;
        }
    }

    public void remove(String owner) throws IOException {
        String key = "device." + owner;
        Object old = records.remove(key);
        try {
            flush();
        } catch (IOException exception) {
            if (old != null) records.put(key, old);
            throw exception;
        }
    }

    public void install(Path temporary, String hash) throws IOException {
        Path target = blob(hash);
        if (Files.exists(target)) {
            if (!GramophoneMediaInfo.inspect(target).hash.equals(hash)) throw new IOException("已存音频校验失败");
            Files.deleteIfExists(temporary);
            return;
        }
        long used = 0;
        try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(root, "*.dgrmp3")) {
            for (Path file : files) {
                GramophoneFiles.check(file);
                used += Files.size(file);
            }
        }
        if (used + Files.size(temporary) > MAX_STORE_BYTES)
            throw new IOException("此存档留声机媒体已达到 512 MiB 上限，请删除不用的音乐并等待回收");
        Files.move(temporary, target, StandardCopyOption.ATOMIC_MOVE);
    }

    public void collect(Set<String> pins) throws IOException {
        collect(pins, System.currentTimeMillis());
    }

    void collect(Set<String> pins, long now) throws IOException {
        Set<String> referenced = new HashSet<String>(pins);
        for (String key : records.stringPropertyNames()) if (key.startsWith("device.")) {
            String source = decode(records.getProperty(key)).source;
            if (source.matches("local:[0-9a-f]{64}")) referenced.add(source.substring(6));
        }
        boolean changed = false;
        try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(root, "*.dgrmp3")) {
            for (Path file : files) {
                String name = file.getFileName()
                    .toString();
                if (!name.matches("[0-9a-f]{64}\\.dgrmp3")) continue;
                String hash = name.substring(0, 64), key = "orphan." + hash;
                if (referenced.contains(hash)) {
                    changed |= records.remove(key) != null;
                    continue;
                }
                long since;
                try {
                    since = Long.parseLong(records.getProperty(key, "0"));
                } catch (NumberFormatException e) {
                    since = 0;
                }
                if (since <= 0 || since > now) {
                    records.setProperty(key, Long.toString(now));
                    changed = true;
                } else if (now - since >= 86400000L) {
                    GramophoneFiles.check(file);
                    Files.deleteIfExists(file);
                    records.remove(key);
                    changed = true;
                }
            }
        }
        if (changed) flush();
    }

    private void flush() throws IOException {
        Path destination = root.resolve("Devices.properties");
        GramophoneFiles.check(destination);
        Path temporary = Files.createTempFile(root, "manifest-", ".tmp");
        try {
            try (java.io.OutputStream output = Files.newOutputStream(temporary)) {
                records.store(new java.io.FilterOutputStream(output) {

                    private int count;

                    @Override
                    public void write(int value) throws IOException {
                        if (++count > 8 * 1024 * 1024) throw new IOException("留声机设备清单超过 8 MiB 上限");
                        out.write(value);
                    }

                    @Override
                    public void write(byte[] value, int offset, int length) throws IOException {
                        if (length > 8 * 1024 * 1024 - count) throw new IOException("留声机设备清单超过 8 MiB 上限");
                        count += length;
                        out.write(value, offset, length);
                    }
                }, "DarkGreyRPG gramophone durable device commits. Back up together with world.");
            }
            try (java.nio.channels.FileChannel channel = java.nio.channels.FileChannel
                .open(temporary, java.nio.file.StandardOpenOption.WRITE)) {
                channel.force(true);
            }
            Files.move(temporary, destination, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
        } finally {
            Files.deleteIfExists(temporary);
        }
    }

    private static String encode(GramophonePacket config) {
        ByteBuf buffer = Unpooled.buffer();
        try {
            config.toBytes(buffer);
            byte[] bytes = new byte[buffer.readableBytes()];
            buffer.readBytes(bytes);
            return java.util.Base64.getEncoder()
                .encodeToString(bytes);
        } finally {
            buffer.release();
        }
    }

    private static GramophonePacket decode(String text) throws IOException {
        try {
            byte[] bytes = java.util.Base64.getDecoder()
                .decode(text);
            ByteBuf buffer = Unpooled.wrappedBuffer(bytes);
            try {
                GramophonePacket config = new GramophonePacket();
                config.fromBytes(buffer);
                return config;
            } finally {
                buffer.release();
            }
        } catch (RuntimeException e) {
            throw new IOException("留声机持久引用索引损坏，已停止写入和回收", e);
        }
    }
}
