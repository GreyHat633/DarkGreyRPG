package darkgrey.rpg.network.message;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

public final class C2SDialogueAction implements IMessage {

    private long sessionId;
    private String nodeId;
    private int choiceIndex;

    public C2SDialogueAction() {}

    public C2SDialogueAction(long sessionId, String nodeId, int choiceIndex) {
        this.sessionId = sessionId;
        this.nodeId = nodeId;
        this.choiceIndex = choiceIndex;
    }

    public long getSessionId() {
        return sessionId;
    }

    public String getNodeId() {
        return nodeId;
    }

    public int getChoiceIndex() {
        return choiceIndex;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        sessionId = buffer.readLong();
        int length = buffer.readUnsignedByte();
        if (length < 1 || length > 96 || buffer.readableBytes() < length + 4) {
            throw new IllegalArgumentException("Invalid Dialogue node ID payload");
        }
        byte[] encoded = new byte[length];
        buffer.readBytes(encoded);
        nodeId = new String(encoded, java.nio.charset.StandardCharsets.UTF_8);
        choiceIndex = buffer.readInt();
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        byte[] encoded = nodeId.getBytes(java.nio.charset.StandardCharsets.UTF_8);
        if (encoded.length < 1 || encoded.length > 96) {
            throw new IllegalArgumentException("Dialogue node ID is too long");
        }
        buffer.writeLong(sessionId);
        buffer.writeByte(encoded.length);
        buffer.writeBytes(encoded);
        buffer.writeInt(choiceIndex);
    }

    public static final class Handler implements IMessageHandler<C2SDialogueAction, IMessage> {

        @Override
        public IMessage onMessage(final C2SDialogueAction message, final MessageContext context) {
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    darkgrey.rpg.runtime.ChatMessages.error(
                        context.getServerHandler().playerEntity,
                        "Legacy Dialogue execution is retired. Use a canonical Session.");
                }
            });
            return null;
        }
    }
}
