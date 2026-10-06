package darkgrey.rpg.project.packages;

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

public final class StoryPackageManagerPacket implements IMessage {

    public boolean response;
    public long sequence;
    public NBTTagCompound data;

    public StoryPackageManagerPacket() {}

    public StoryPackageManagerPacket(boolean response, long sequence, NBTTagCompound data) {
        this.response = response;
        this.sequence = sequence;
        this.data = (NBTTagCompound) data.copy();
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 13 || buffer.readableBytes() > 131085)
            throw new IllegalArgumentException("Invalid package manager packet");
        int kind = buffer.readUnsignedByte();
        response = kind == 1;
        sequence = buffer.readLong();
        int length = buffer.readInt();
        if (kind > 1 || sequence <= 0 || length <= 0 || length != buffer.readableBytes() || !response && length > 4096)
            throw new IllegalArgumentException("Invalid package manager request");
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        try {
            data = CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(response ? 1048576 : 16384));
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Invalid package manager payload", invalid);
        }
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        try {
            byte[] bytes = CompressedStreamTools.compress(data);
            if (bytes.length > (response ? 131072 : 4096))
                throw new IllegalArgumentException("Package manager payload exceeds budget");
            buffer.writeByte(response ? 1 : 0);
            buffer.writeLong(sequence);
            buffer.writeInt(bytes.length);
            buffer.writeBytes(bytes);
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException(invalid);
        }
    }

    public static final class Server implements IMessageHandler<StoryPackageManagerPacket, IMessage> {

        private static StoryPackageLoader current;
        private static StoryPackageManagerService manager;
        private static final java.util.Map<EntityPlayerMP, Long> last = new java.util.WeakHashMap<EntityPlayerMP, Long>();

        public static void notifyInventory(EntityPlayerMP player) {
            if (manager == null || current != DarkGreyRpg.getStoryPackageLoader()) return;
            NBTTagCompound notice = manager.notification(player, player.canCommandSenderUseCommand(2, "dgr"));
            if (notice != null) DialogueNetwork.CHANNEL.sendTo(new StoryPackageManagerPacket(true, 1, notice), player);
        }

        public static void forget(EntityPlayerMP player) {
            if (manager != null) manager.forget(player);
            synchronized (last) {
                last.remove(player);
            }
        }

        @Override
        public IMessage onMessage(final StoryPackageManagerPacket message, MessageContext context) {
            if (message.response) return null;
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            synchronized (last) {
                long now = System.nanoTime();
                Long previous = last.get(player);
                if (message.data.getInteger("action") != StoryPackageManagerService.CLOSE && previous != null
                    && now - previous < 100000000L) return null;
                last.put(player, now);
            }
            MainThreadScheduler.scheduleServer(() -> {
                StoryPackageLoader loader = DarkGreyRpg.getStoryPackageLoader();
                if (loader == null) return;
                if (current != loader) {
                    current = loader;
                    manager = new StoryPackageManagerService(loader, new ForgeRuntime(loader));
                }
                NBTTagCompound result = manager
                    .request(player, player.canCommandSenderUseCommand(2, "dgr"), message.sequence, message.data);
                DialogueNetwork.CHANNEL.sendTo(new StoryPackageManagerPacket(true, message.sequence, result), player);
            });
            return null;
        }
    }

    private static final class ForgeRuntime implements StoryPackageManagerService.RuntimeAccess {

        private final StoryPackageLoader loader;

        ForgeRuntime(StoryPackageLoader loader) {
            this.loader = loader;
        }

        private net.minecraft.world.storage.MapStorage storage() {
            return net.minecraft.server.MinecraftServer.getServer()
                .worldServerForDimension(0).mapStorage;
        }

        @Override
        public void reload() {
            StoryPackageRuntimeReloader.Result result = StoryPackageRuntimeReloader
                .reload(DarkGreyRpg.getProjectRepository(), loader);
            if (result.isPackageSetCommitted())
                StoryPackageGenerationLifecycle.reconcile(storage(), loader.getPackages());
        }

        @Override
        public void retire(java.util.Map<String, LoadedStoryPackage> retained) {
            try {
                if (retained.isEmpty()) DarkGreyRpg.getProjectRepository()
                    .installUnavailableSnapshot("Container deletion in progress");
                else DarkGreyRpg.getProjectRepository()
                    .installSnapshot(StoryPackageSnapshotMerger.merge(retained));
                StoryPackageGenerationLifecycle.reconcile(storage(), retained);
            } catch (darkgrey.rpg.project.ProjectLoadException failure) {
                throw new IllegalStateException(failure);
            }
        }

        @Override
        public int activeCount(java.util.Set<String> stories) {
            try {
                NBTTagCompound state = new NBTTagCompound();
                darkgrey.rpg.session.persistence.CanonicalSessionSavedData.get(storage())
                    .writeToNBT(state);
                int count = 0;
                for (darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot story : darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec
                    .decode(state)
                    .getStories())
                    if (stories.contains(story.getStoryId())
                        && story.getStatus() == darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus.ACTIVE)
                        count++;
                return count;
            } catch (RuntimeException unavailable) {
                return -1;
            }
        }
    }

    public static final class Client implements IMessageHandler<StoryPackageManagerPacket, IMessage> {

        @Override
        public IMessage onMessage(final StoryPackageManagerPacket message, MessageContext context) {
            if (!message.response) return null;
            final Object connection = context.netHandler;
            MainThreadScheduler.scheduleClient(() -> {
                if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                    DarkGreyRpg.proxy.acceptPackageManager(message.sequence, message.data);
            });
            return null;
        }
    }
}
