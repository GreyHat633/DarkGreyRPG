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
            MainThreadScheduler.scheduleClient(() -> {
                if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                    darkgrey.rpg.client.TaskPresentationPages.accept(message.data);
            });
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
                if (gate.requests >= 10 || gate.queued >= 2 || QUEUED.get() >= 64) return null;
                gate.requests++;
                gate.queued++;
                QUEUED.incrementAndGet();
            }
            MainThreadScheduler.scheduleServer(() -> {
                try {
                    if (player.playerNetServerHandler != connection || connection.playerEntity != player) return;
                    NBTTagCompound result = darkgrey.rpg.creator.TaskPageServer.project(player, message.data);
                    if (result == null) return;
                    TaskPresentationPage packet = new TaskPresentationPage(true, result);
                    io.netty.buffer.ByteBuf bytes = io.netty.buffer.Unpooled.buffer();
                    try {
                        packet.toBytes(bytes);
                    } finally {
                        bytes.release();
                    }
                    DialogueNetwork.CHANNEL.sendTo(packet, player);
                } catch (RuntimeException invalid) { /* Invalid/stale request cannot change gameplay. */ } finally {
                    synchronized (GATES) {
                        gate.queued--;
                        QUEUED.decrementAndGet();
                    }
                }
            });
            return null;
        }

        private static final class Gate {

            int queued, requests;
            long window;
        }
    }
}
