package darkgrey.rpg.network.message;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.DialogueClientController;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

public final class S2CDialogueClose implements IMessage {

    private long sessionId;
    private String result;

    public S2CDialogueClose() {}

    public S2CDialogueClose(long sessionId, String result) {
        this.sessionId = sessionId;
        this.result = result;
    }

    public long getSessionId() {
        return sessionId;
    }

    public String getResult() {
        return result;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        sessionId = buffer.readLong();
        result = ByteBufUtils.readUTF8String(buffer);
        if (result.length() > 96) {
            throw new IllegalArgumentException("Dialogue result is too long");
        }
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeLong(sessionId);
        ByteBufUtils.writeUTF8String(buffer, result);
    }

    public static final class Handler implements IMessageHandler<S2CDialogueClose, IMessage> {

        @Override
        public IMessage onMessage(final S2CDialogueClose message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    DialogueClientController.close(message.sessionId, message.result);
                }
            });
            return null;
        }
    }
}
