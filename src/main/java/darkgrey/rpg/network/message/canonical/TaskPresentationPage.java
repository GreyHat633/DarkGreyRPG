package darkgrey.rpg.network.message.canonical;

import java.util.Map;
import java.util.WeakHashMap;
import java.util.concurrent.atomic.AtomicInteger;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTSizeTracker;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Read-only authorized candidate/history pages. Neither operation can execute a task. */
public final class TaskPresentationPage implements IMessage {

    public boolean response;
    public NBTTagCompound data;

    public TaskPresentationPage() {}

    public TaskPresentationPage(boolean response, NBTTagCompound data) {
        this.response = response;
        this.data = (NBTTagCompound) data.copy();
    }

    /** Same request identity, explicit failure; never represents an empty successful page. */
    public static TaskPresentationPage failureResponse(NBTTagCompound request, String reason) {
        NBTTagCompound result = (NBTTagCompound) request.copy();
        result.setString("error", reason);
        return new TaskPresentationPage(true, result);
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        try {
            byte[] bytes = CompressedStreamTools.compress(data);
            int max = response ? 131072 : 2048;
            if (bytes.length > max) throw new IllegalArgumentException("Task page encoding exceeds budget");
            CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(response ? 524288L : 8192L));
            buffer.writeBoolean(response);
            buffer.writeInt(bytes.length);
            buffer.writeBytes(bytes);
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Cannot encode task page", invalid);
        }
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 5) throw new IllegalArgumentException("Truncated task page");
        int flag = buffer.readUnsignedByte();
        if (flag > 1) throw new IllegalArgumentException("Invalid task page flag");
        response = flag == 1;
        int length = buffer.readInt();
        if (length < 1 || length > (response ? 131072 : 2048) || length != buffer.readableBytes())
            throw new IllegalArgumentException("Invalid task page length");
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        try {
            data = CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(response ? 524288L : 8192L));
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Invalid task page NBT", invalid);
        }
    }

    public static final class Client implements IMessageHandler<TaskPresentationPage, IMessage> {

        @Override
        public IMessage onMessage(final TaskPresentationPage message, MessageContext context) {
            if (!message.response) return null;
            final Object connection = context.netHandler;
            MainThreadScheduler.scheduleClient(connection, () -> {
                if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                    darkgrey.rpg.client.TaskPresentationPages.accept(message.data);
            }, () -> MainThreadScheduler.rejectClient(connection));
            return null;
        }
    }

    public static final class Server implements IMessageHandler<TaskPresentationPage, IMessage> {

        private static final AtomicInteger QUEUED = new AtomicInteger();
        private static final Map<Object, Gate> GATES = new WeakHashMap<Object, Gate>();

        @Override
        public IMessage onMessage(final TaskPresentationPage message, MessageContext context) {
            if (message.response) return null;
            final net.minecraft.network.NetHandlerPlayServer connection = context.getServerHandler();
            final EntityPlayerMP player = connection.playerEntity;
            final Gate gate;
            synchronized (GATES) {
                Gate current = GATES.get(connection);
                if (current == null) {
                    current = new Gate();
                    GATES.put(connection, current);
                }
                gate = current;
                long now = System.nanoTime();
                if (now - gate.window > 1000000000L) {
                    gate.window = now;
                    gate.requests = 0;
                }
                if (gate.requests >= 10 || gate.queued >= 2 || QUEUED.get() >= 64) {
                    reject(connection, player, message.data, "服务器繁忙，请稍后重试。");
                    return null;
                }
                gate.requests++;
                gate.queued++;
                QUEUED.incrementAndGet();
            }
            final java.util.concurrent.atomic.AtomicBoolean reserved = new java.util.concurrent.atomic.AtomicBoolean(
                true);
            Runnable release = () -> {
                if (reserved.compareAndSet(true, false)) synchronized (GATES) {
                    gate.queued--;
                    QUEUED.decrementAndGet();
                }
            };
            MainThreadScheduler.scheduleServer(player, () -> {
                try {
                    if (player.playerNetServerHandler != connection || connection.playerEntity != player) return;
                    NBTTagCompound result = darkgrey.rpg.creator.TaskPageServer.project(player, message.data);
                    if (result == null) {
                        reject(connection, player, message.data, "此页的任务或资源状态已变化，请刷新后重试。");
                        return;
                    }
                    TaskPresentationPage packet = new TaskPresentationPage(true, result);
                    io.netty.buffer.ByteBuf bytes = io.netty.buffer.Unpooled.buffer();
                    try {
                        packet.toBytes(bytes);
                    } finally {
                        bytes.release();
                    }
                    DialogueNetwork.CHANNEL.sendTo(packet, player);
                } catch (RuntimeException invalid) {
                    reject(connection, player, message.data, "此页读取失败，请稍后重试。");
                    MainThreadScheduler.reportFailure("task-page", invalid);
                } finally {
                    release.run();
                }
            }, () -> {
                release.run();
                reject(connection, player, message.data, "本次请求未执行，请稍后重试。");
            });
            return null;
        }

        private static void reject(net.minecraft.network.NetHandlerPlayServer connection, EntityPlayerMP player,
            NBTTagCompound request, String reason) {
            if (player.playerNetServerHandler == connection && connection.playerEntity == player
                && connection.netManager.isChannelOpen())
                DialogueNetwork.CHANNEL.sendTo(failureResponse(request, reason), player);
        }

        private static final class Gate {

            int queued, requests;
            long window;
        }
    }
}
