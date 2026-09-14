package darkgrey.rpg.network.message.canonical;

import java.nio.charset.StandardCharsets;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.creator.CanonicalTaskPresentationServer;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** An intent bound to a server-issued presentation revision; never carries inventory or progress. */
public final class CanonicalTaskSubmit implements IMessage {

    private long revision;
    private String taskId;
    private String objectiveId;

    public CanonicalTaskSubmit() {}

    public CanonicalTaskSubmit(long revision, String taskId, String objectiveId) {
        if (revision < 0) throw new IllegalArgumentException("Invalid Task revision.");
        this.revision = revision;
        this.taskId = taskId;
        this.objectiveId = objectiveId;
    }

    public void toBytes(ByteBuf buffer) {
        buffer.writeLong(revision);
        write(buffer, taskId);
        write(buffer, objectiveId);
    }

    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 12) throw new IllegalArgumentException("Truncated Task submit.");
        revision = buffer.readLong();
        taskId = read(buffer);
        objectiveId = read(buffer);
        if (revision < 0 || buffer.isReadable()) throw new IllegalArgumentException("Invalid Task submit.");
    }

    private static void write(ByteBuf buffer, String text) {
        if (text == null) throw new IllegalArgumentException("Missing Task identity.");
        byte[] bytes = text.getBytes(StandardCharsets.UTF_8);
        if (bytes.length == 0 || bytes.length > 1024) throw new IllegalArgumentException("Invalid Task identity size.");
        buffer.writeShort(bytes.length);
        buffer.writeBytes(bytes);
    }

    private static String read(ByteBuf buffer) {
        if (buffer.readableBytes() < 2) throw new IllegalArgumentException("Truncated Task identity.");
        int size = buffer.readUnsignedShort();
        if (size == 0 || size > 1024 || size > buffer.readableBytes())
            throw new IllegalArgumentException("Invalid Task identity size.");
        byte[] bytes = new byte[size];
        buffer.readBytes(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }

    public static final class Handler implements IMessageHandler<CanonicalTaskSubmit, IMessage> {

        public IMessage onMessage(final CanonicalTaskSubmit message, final MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                public void run() {
                    try {
                        if (!CanonicalTaskPresentationServer
                            .submit(player, message.revision, message.taskId, message.objectiveId))
                            player.addChatMessage(new net.minecraft.util.ChatComponentText("物品不足或目标状态已变化，请查看最新任务状态。"));
                    } catch (RuntimeException failure) {
                        player.addChatMessage(new net.minecraft.util.ChatComponentText("提交未完成，请重试。"));
                        org.apache.logging.log4j.LogManager.getLogger("DGR Task")
                            .warn("Task submit failed", failure);
                    }
                }
            });
            return null;
        }
    }
}
