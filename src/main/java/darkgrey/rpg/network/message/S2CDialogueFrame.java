package darkgrey.rpg.network.message;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import cpw.mods.fml.common.network.ByteBufUtils;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.DialogueClientController;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

public final class S2CDialogueFrame implements IMessage {

    private long sessionId;
    private String dialogueId;
    private String nodeId;
    private String speakerName;
    private String text;
    private List<String> choices = Collections.emptyList();
    private boolean canContinue;

    public S2CDialogueFrame() {}

    public S2CDialogueFrame(long sessionId, String dialogueId, String nodeId, String speakerName, String text,
        List<String> choices, boolean canContinue) {
        this.sessionId = sessionId;
        this.dialogueId = dialogueId;
        this.nodeId = nodeId;
        this.speakerName = speakerName;
        this.text = text;
        this.choices = Collections.unmodifiableList(new ArrayList<String>(choices));
        this.canContinue = canContinue;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        sessionId = buffer.readLong();
        dialogueId = readLimitedString(buffer, 96);
        nodeId = readLimitedString(buffer, 96);
        speakerName = readLimitedString(buffer, 256);
        text = readLimitedString(buffer, 32767);
        int count = buffer.readUnsignedByte();
        if (count > 32) {
            throw new IllegalArgumentException("Too many Dialogue choices");
        }
        List<String> decodedChoices = new ArrayList<String>();
        for (int index = 0; index < count; index++) {
            decodedChoices.add(readLimitedString(buffer, 2048));
        }
        choices = Collections.unmodifiableList(decodedChoices);
        canContinue = buffer.readBoolean();
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeLong(sessionId);
        writeLimitedString(buffer, dialogueId, 96);
        writeLimitedString(buffer, nodeId, 96);
        writeLimitedString(buffer, speakerName, 256);
        writeLimitedString(buffer, text, 32767);
        if (choices.size() > 32) {
            throw new IllegalArgumentException("Too many Dialogue choices");
        }
        buffer.writeByte(choices.size());
        for (String choice : choices) {
            writeLimitedString(buffer, choice, 2048);
        }
        buffer.writeBoolean(canContinue);
    }

    private static String readLimitedString(ByteBuf buffer, int maximumLength) {
        String value = ByteBufUtils.readUTF8String(buffer);
        if (value.length() > maximumLength) {
            throw new IllegalArgumentException("Dialogue string exceeds limit");
        }
        return value;
    }

    private static void writeLimitedString(ByteBuf buffer, String value, int maximumLength) {
        if (value.length() > maximumLength) {
            throw new IllegalArgumentException("Dialogue string exceeds limit");
        }
        ByteBufUtils.writeUTF8String(buffer, value);
    }

    public long getSessionId() {
        return sessionId;
    }

    public String getDialogueId() {
        return dialogueId;
    }

    public String getNodeId() {
        return nodeId;
    }

    public String getSpeakerName() {
        return speakerName;
    }

    public String getText() {
        return text;
    }

    public List<String> getChoices() {
        return choices;
    }

    public boolean canContinue() {
        return canContinue;
    }

    public static final class Handler implements IMessageHandler<S2CDialogueFrame, IMessage> {

        @Override
        public IMessage onMessage(final S2CDialogueFrame message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    DialogueClientController.showFrame(message);
                }
            });
            return null;
        }
    }
}
