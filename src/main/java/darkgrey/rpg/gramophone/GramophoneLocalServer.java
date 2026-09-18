package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.tileentity.TileEntity;

import darkgrey.rpg.network.MainThreadScheduler;

/** World checks on the server thread; all file, hash, and decoder work on one bounded worker. */
public final class GramophoneLocalServer {

    private static final ThreadPoolExecutor IO = new ThreadPoolExecutor(
        1,
        1,
        30,
        TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(64),
        task -> {
            Thread t = new Thread(task, "DGR-Gramophone-ServerIO");
            t.setDaemon(true);
            return t;
        });
    private static final Map<Path, GramophoneBlobStore> STORES = new HashMap<Path, GramophoneBlobStore>(); // worker
                                                                                                           // only
    private static final Map<UUID, Upload> UPLOADS = new HashMap<UUID, Upload>(); // server thread
    private static final Set<String> BUSY = new HashSet<String>();
    private static final Set<String> COMMITTING = new HashSet<String>();
    private static Path mediaRoot;
    private static long lastMaintenance;
    private static boolean maintenancePending;

    private static final class Upload {

        final GramophoneMediaPacket request;
        final Object world;
        final Path store;
        Path temporary;
        int offset;
        long touched = System.nanoTime();
        volatile boolean cancelled;

        Upload(GramophoneMediaPacket request, EntityPlayerMP player, Path store) {
            this.request = request;
            this.world = player.worldObj;
            this.store = store;
        }
    }

    public static void initialize(Path runtimeRoot) {
        mediaRoot = runtimeRoot.resolve("Media/Gramophone");
    }

    private static Path storePath(net.minecraft.world.World world) {
        String identity = world.getSaveHandler()
            .getWorldDirectory()
            .toPath()
            .toAbsolutePath()
            .normalize()
            .toString();
        return mediaRoot.resolve(
            UUID.nameUUIDFromBytes(identity.getBytes(java.nio.charset.StandardCharsets.UTF_8))
                .toString());
    }

    private static GramophoneBlobStore store(Path path) throws IOException {
        GramophoneBlobStore value = STORES.get(path);
        if (value == null) {
            value = new GramophoneBlobStore(path);
            STORES.put(path, value);
        }
        return value;
    }

    public static boolean committing(GramophonePacket request) {
        return COMMITTING.contains(request.key());
    }

    public static TileGramophone target(EntityPlayerMP player, GramophonePacket request, boolean edit)
        throws IOException {
        if (!player.playerNetServerHandler.netManager.isChannelOpen()
            || player.worldObj.provider.dimensionId != request.dimension
            || !player.worldObj.blockExists(request.x, request.y, request.z)) throw new IOException("设备已离线或所在区块已卸载");
        TileEntity found = player.worldObj.getTileEntity(request.x, request.y, request.z);
        if (!(found instanceof TileGramophone)) throw new IOException("留声机已被移除");
        TileGramophone tile = (TileGramophone) found;
        if (!tile.ready || !tile.instance.equals(request.instance) || tile.revision != request.revision)
            throw new IOException("配置已变化或正在恢复，请重新打开");
        if (edit) {
            if (!GramophoneServer.allowed(player)
                || player.getDistanceSq(request.x + .5, request.y + .5, request.z + .5) > 64)
                throw new IOException("需要 OP/创造权限并在设备 8 格内");
            if (request.radius < 0 || request.radius > GramophoneServer.MAX_RADIUS)
                throw new IOException("范围须为 0–128 格");
            if (committing(request)) throw new IOException("另一项保存正在提交，请稍后重试");
        } else if (!GramophonePlayback.contains(
            player.dimension,
            request.dimension,
            request.x,
            request.y,
            request.z,
            tile.radius,
            player.posX,
            player.posY,
            player.posZ)) throw new IOException("播放器已离开设备范围");
        return tile;
    }

    public static void accept(EntityPlayerMP player, GramophoneMediaPacket packet) {
        UUID id = player.getUniqueID();
        String busyKey = id + ":" + packet.token;
        if (packet.operation == GramophoneMediaPacket.CANCEL) {
            Upload upload = UPLOADS.get(id);
            if (upload != null && upload.request.token.equals(packet.token)) cancel(id, upload);
            return;
        }
        if (BUSY.contains(busyKey)) return;
        try {
            if (packet.operation == GramophoneMediaPacket.FETCH) {
                TileGramophone tile = target(player, packet.device, false);
                if (!tile.source.equals("local:" + packet.hash)) throw new IOException("此设备没有请求的音乐");
                Path path = storePath(player.worldObj);
                BUSY.add(busyKey);
                submit(() -> {
                    try {
                        Path blob = store(path).blob(packet.hash);
                        if (packet.offset == 0 && !GramophoneMediaInfo.inspect(blob).hash.equals(packet.hash))
                            throw new IOException("服务端音频校验失败");
                        int length = (int) Files.size(blob);
                        if (packet.offset > length) throw new IOException("无效下载位置");
                        byte[] bytes = new byte[Math.min(GramophoneMediaPacket.CHUNK_BYTES, length - packet.offset)];
                        try (java.io.RandomAccessFile file = new java.io.RandomAccessFile(blob.toFile(), "r")) {
                            file.seek(packet.offset);
                            file.readFully(bytes);
                        }
                        packet.operation = GramophoneMediaPacket.DATA;
                        packet.total = length;
                        packet.data = bytes;
                        reply(player, packet);
                    } catch (Exception e) {
                        error(player, packet, e);
                    }
                }, player, packet);
                return;
            }
            if (packet.operation != GramophoneMediaPacket.UPLOAD && packet.operation != GramophoneMediaPacket.CHUNK)
                return;
            target(player, packet.device, true);
            Upload upload;
            if (packet.operation == GramophoneMediaPacket.UPLOAD) {
                if (packet.total <= 0 || packet.offset != 0 || packet.data.length != 0) throw new IOException("无效上传申请");
                Upload prior = UPLOADS.get(id);
                if (prior != null) cancel(id, prior);
                if (UPLOADS.size() >= 8) throw new IOException("上传会话已满，请稍后重试");
                upload = new Upload(packet, player, storePath(player.worldObj));
                UPLOADS.put(id, upload);
            } else {
                upload = UPLOADS.get(id);
                if (upload == null || upload.world != player.worldObj
                    || !upload.request.token.equals(packet.token)
                    || !upload.request.hash.equals(packet.hash)
                    || upload.request.total != packet.total
                    || !upload.request.device.key()
                        .equals(packet.device.key())
                    || upload.request.device.revision != packet.device.revision) throw new IOException("上传会话已失效");
            }
            upload.touched = System.nanoTime();
            BUSY.add(busyKey);
            final Upload active = upload;
            submit(() -> {
                try {
                    if (active.cancelled) throw new IOException("上传已取消");
                    GramophoneBlobStore storage = store(active.store);
                    if (packet.operation == GramophoneMediaPacket.UPLOAD) {
                        Path existing = storage.blob(packet.hash);
                        if (Files.exists(existing) && GramophoneMediaInfo.inspect(existing).hash.equals(packet.hash))
                            active.offset = packet.total;
                        else active.temporary = storage.temporary();
                    } else {
                        if (packet.offset != active.offset || packet.data.length == 0
                            || packet.data.length > packet.total - active.offset) throw new IOException("上传分块顺序或长度不符");
                        try (java.io.OutputStream output = Files
                            .newOutputStream(active.temporary, java.nio.file.StandardOpenOption.APPEND)) {
                            output.write(packet.data);
                        }
                        active.offset += packet.data.length;
                    }
                    if (active.offset == active.request.total) {
                        if (active.temporary != null) {
                            GramophoneMediaInfo info = GramophoneMediaInfo.inspect(active.temporary);
                            if (info.bytes != active.request.total || !info.hash.equals(active.request.hash))
                                throw new IOException("上传音频长度或指纹校验失败");
                            if (active.cancelled) throw new IOException("上传已取消");
                            storage.install(active.temporary, info.hash);
                            active.temporary = null;
                        }
                        MainThreadScheduler.scheduleServer(() -> {
                            BUSY.remove(busyKey);
                            if (active.cancelled || UPLOADS.get(id) != active) return;
                            try {
                                TileGramophone tile = target(player, active.request.device, true);
                                GramophonePacket config = active.request.device;
                                config.source = "local:" + active.request.hash;
                                UPLOADS.remove(id);
                                commit(player, tile, config, active.request);
                            } catch (IOException e) {
                                cancel(id, active);
                                errorNow(player, active.request, e.getMessage());
                            }
                        });
                    } else {
                        packet.operation = GramophoneMediaPacket.ACK;
                        packet.offset = active.offset;
                        packet.data = new byte[0];
                        reply(player, packet);
                    }
                } catch (Exception e) {
                    MainThreadScheduler.scheduleServer(() -> cancel(id, active));
                    error(player, packet, e);
                }
            }, player, packet);
        } catch (IOException e) {
            errorNow(player, packet, e.getMessage());
        }
    }

    private static void submit(Runnable task, EntityPlayerMP player, GramophoneMediaPacket packet) {
        try {
            IO.execute(task);
        } catch (java.util.concurrent.RejectedExecutionException e) {
            BUSY.remove(player.getUniqueID() + ":" + packet.token);
            errorNow(player, packet, "媒体队列已满，请稍后重试");
        }
    }

    private static void reply(EntityPlayerMP player, GramophoneMediaPacket packet) {
        MainThreadScheduler.scheduleServer(() -> {
            BUSY.remove(player.getUniqueID() + ":" + packet.token);
            if (player.playerNetServerHandler.netManager.isChannelOpen())
                GramophoneNetwork.CHANNEL.sendTo(packet, player);
        });
    }

    private static void error(EntityPlayerMP player, GramophoneMediaPacket packet, Exception e) {
        packet.data = new byte[0];
        packet.operation = GramophoneMediaPacket.ERROR;
        packet.message = readable(e);
        reply(player, packet);
    }

    private static void errorNow(EntityPlayerMP player, GramophoneMediaPacket packet, String message) {
        if (!player.playerNetServerHandler.netManager.isChannelOpen()) return;
        packet.data = new byte[0];
        packet.operation = GramophoneMediaPacket.ERROR;
        packet.message = message == null ? "媒体操作失败" : message;
        GramophoneNetwork.CHANNEL.sendTo(packet, player);
    }

    private static String readable(Exception e) {
        String text = e.getMessage();
        return text == null ? "媒体操作失败" : text.substring(0, Math.min(text.length(), 220));
    }

    private static void cancel(UUID player, Upload upload) {
        upload.cancelled = true;
        if (UPLOADS.get(player) == upload) UPLOADS.remove(player);
        try {
            IO.execute(() -> { if (upload.temporary != null) GramophoneFiles.retire(upload.temporary); });
        } catch (java.util.concurrent.RejectedExecutionException ignored) { /*
                                                                             * Stale incoming files are recovered on
                                                                             * next startup.
                                                                             */ }
    }

    public static void tick() {
        long now = System.nanoTime();
        for (Map.Entry<UUID, Upload> entry : new HashMap<UUID, Upload>(UPLOADS).entrySet())
            if (now - entry.getValue().touched > TimeUnit.SECONDS.toNanos(60)) cancel(entry.getKey(), entry.getValue());
        if (!maintenancePending && now - lastMaintenance > TimeUnit.MINUTES.toNanos(1)) {
            lastMaintenance = now;
            maintenancePending = true;
            Set<String> pins = new HashSet<String>();
            for (Upload upload : UPLOADS.values()) pins.add(upload.request.hash);
            try {
                IO.execute(() -> {
                    try {
                        for (GramophoneBlobStore storage : STORES.values()) storage.collect(pins);
                    } catch (IOException e) {
                        darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone delayed collection failed", e);
                    } finally {
                        MainThreadScheduler.scheduleServer(() -> maintenancePending = false);
                    }
                });
            } catch (java.util.concurrent.RejectedExecutionException e) {
                maintenancePending = false;
            }
        }
    }

    public static void commit(EntityPlayerMP player, TileGramophone tile, GramophonePacket config,
        GramophoneMediaPacket transfer) {
        String key = config.key();
        COMMITTING.add(key);
        config.revision = tile.revision + 1;
        Path path = storePath(tile.getWorldObj());
        try {
            IO.execute(() -> {
                try {
                    store(path).commit(config);
                    MainThreadScheduler.scheduleServer(() -> {
                        COMMITTING.remove(key);
                        if (tile.isInvalid()) {
                            removed(tile);
                            if (transfer != null) errorNow(player, transfer, "设备已被移除，未替换音乐");
                            return;
                        }
                        if (GramophoneServer.loaded(tile)) apply(tile, config);
                        // A commit reserved before chunk unload remains durable; never force the chunk back in.
                        GramophonePacket result = config;
                        result.operation = GramophonePacket.RESULT;
                        result.status = "已保存";
                        if (player.playerNetServerHandler.netManager.isChannelOpen()) {
                            GramophoneNetwork.CHANNEL.sendTo(result, player);
                            if (transfer != null) {
                                transfer.operation = GramophoneMediaPacket.COMMITTED;
                                transfer.data = new byte[0];
                                transfer.offset = transfer.total;
                                GramophoneNetwork.CHANNEL.sendTo(transfer, player);
                            }
                        }
                    });
                } catch (Exception e) {
                    MainThreadScheduler.scheduleServer(() -> {
                        COMMITTING.remove(key);
                        config.revision = tile.revision;
                        config.operation = GramophonePacket.RESULT;
                        config.status = readable(e);
                        if (player.playerNetServerHandler.netManager.isChannelOpen())
                            GramophoneNetwork.CHANNEL.sendTo(config, player);
                        if (transfer != null) errorNow(player, transfer, readable(e));
                    });
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            COMMITTING.remove(key);
            config.revision = tile.revision;
            config.operation = GramophonePacket.RESULT;
            config.status = "保存队列已满，请稍后重试";
            GramophoneNetwork.CHANNEL.sendTo(config, player);
        }
    }

    private static void apply(TileGramophone tile, GramophonePacket config) {
        tile.source = config.source;
        tile.radius = config.radius;
        tile.enabled = config.enabled;
        tile.redstone = config.redstone;
        tile.revision = config.revision;
        tile.markDirty();
        tile.getWorldObj()
            .markBlockForUpdate(tile.xCoord, tile.yCoord, tile.zCoord);
        GramophoneServer.changed(tile);
    }

    public static void restore(TileGramophone tile) {
        if (mediaRoot == null || tile.getWorldObj() == null || tile.getWorldObj().isRemote) return;
        tile.restoring = true;
        GramophonePacket current = tile.snapshot(GramophonePacket.STATE);
        Path path = storePath(tile.getWorldObj());
        try {
            IO.execute(() -> {
                try {
                    GramophonePacket saved = store(path).saved(current.key());
                    MainThreadScheduler.scheduleServer(() -> {
                        tile.restoring = false;
                        if (tile.isInvalid() || !GramophoneServer.loaded(tile)) return;
                        if (saved != null && saved.revision >= tile.revision) apply(tile, saved);
                        tile.ready = true;
                    });
                } catch (Exception e) {
                    MainThreadScheduler.scheduleServer(() -> {
                        tile.restoring = false;
                        tile.retryRestore = System.nanoTime() + TimeUnit.SECONDS.toNanos(30);
                        tile.recoveryError = readable(e);
                    });
                    darkgrey.rpg.DarkGreyRpg.LOG.error("Gramophone durable configuration recovery failed", e);
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            tile.restoring = false;
            tile.retryRestore = System.nanoTime() + TimeUnit.SECONDS.toNanos(5);
        }
    }

    public static void removed(TileGramophone tile) {
        if (tile.getWorldObj() == null || tile.getWorldObj().isRemote) return;
        GramophonePacket tombstone = tile.snapshot(GramophonePacket.STATE);
        tombstone.source = "";
        tombstone.enabled = false;
        tombstone.revision++;
        Path path = storePath(tile.getWorldObj());
        try {
            IO.execute(() -> {
                try {
                    store(path).commit(tombstone);
                    store(path).collect(new HashSet<String>());
                } catch (Exception e) {
                    darkgrey.rpg.DarkGreyRpg.LOG.error("Gramophone reference removal failed; retained for recovery", e);
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone removal queue full; reference retained");
        }
    }

    public static void unload(net.minecraft.world.World world) {
        if (world.isRemote || world.provider.dimensionId != 0 || mediaRoot == null) return;
        Path path = storePath(world);
        for (Map.Entry<UUID, Upload> entry : new HashMap<UUID, Upload>(UPLOADS).entrySet())
            if (entry.getValue().store.equals(path)) cancel(entry.getKey(), entry.getValue());
        try {
            IO.execute(() -> STORES.remove(path));
        } catch (java.util.concurrent.RejectedExecutionException e) {
            darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone store close queue full");
        }
    }
}
