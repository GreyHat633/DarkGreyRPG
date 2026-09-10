package darkgrey.rpg.network.message;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.task.journal.CanonicalJournalService;
import io.netty.buffer.ByteBuf;

public final class C2SQuestJournalRequest implements IMessage {

    @Override
    public void fromBytes(ByteBuf buffer) {}

    @Override
    public void toBytes(ByteBuf buffer) {}

    public static final class Handler implements IMessageHandler<C2SQuestJournalRequest, IMessage> {

        @Override
        public IMessage onMessage(C2SQuestJournalRequest message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    new CanonicalJournalService(DarkGreyRpg.getCanonicalTaskManager()).openJournal(player);
                }
            });
            return null;
        }
    }
}
