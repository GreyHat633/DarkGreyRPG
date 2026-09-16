package darkgrey.rpg.network.message.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.graph.canonical.CanonicalMediaReference;
import io.netty.buffer.ByteBuf;

/** Server-authoritative ownership and preload metadata for Story Package media. */
public final class StoryMediaPlan implements IMessage {

    public static final int MAX_PACKAGES = 10;
    /** Wire bound; the cache may still admit ten packages independently. */
    public static final int MAX_DESCRIPTORS_PER_PACKET = 1;
    public static final int MAX_REFS_PER_PACKAGE = 4096;
    public static final int MAX_RUNNING_IDS = 4096;
    public static final int MAX_CHUNKS = 65535;
    private static final int MAX_FINGERPRINT_BYTES = 128;
    private int chunkIndex;
    private int chunkCount;
    private long revision;
    private List<Descriptor> descriptors;
    private Set<String> runningPackageIds;

    public StoryMediaPlan() {}

    public StoryMediaPlan(List<Descriptor> descriptors, Set<String> runningPackageIds) {
        this(0, 1, descriptors, runningPackageIds);
    }

    public StoryMediaPlan(int chunkIndex, int chunkCount, List<Descriptor> descriptors, Set<String> runningPackageIds) {
        this(0L, chunkIndex, chunkCount, descriptors, runningPackageIds);
    }

    public StoryMediaPlan(long revision, int chunkIndex, int chunkCount, List<Descriptor> descriptors,
        Set<String> runningPackageIds) {
        if (revision < 0) throw invalid("negative plan revision");
        this.chunkIndex = chunkIndex;
        this.chunkCount = chunkCount;
        this.revision = revision;
        validateChunk(chunkIndex, chunkCount);
        this.descriptors = freezeDescriptors(descriptors);
        this.runningPackageIds = freezeIds(runningPackageIds);
    }

    public int getChunkIndex() {
        return chunkIndex;
    }

    public int getChunkCount() {
        return chunkCount;
    }

    public long getRevision() {
        return revision;
    }

    public List<Descriptor> getDescriptors() {
        return descriptors;
    }

    public Set<String> getRunningPackageIds() {
        return runningPackageIds;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validateChunk(chunkIndex, chunkCount);
        if (descriptors.size() > MAX_DESCRIPTORS_PER_PACKET) throw invalid("too many package descriptors");
        buffer.writeByte(1);
        buffer.writeLong(revision);
        buffer.writeShort(chunkIndex);
        buffer.writeShort(chunkCount);
        buffer.writeByte(descriptors.size());
        for (Descriptor descriptor : descriptors) descriptor.write(buffer);
        buffer.writeShort(runningPackageIds.size());
        for (String id : runningPackageIds)
            CanonicalSessionNetworkCodec.writeField(buffer, id, "running_package_id", 96);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 14) throw invalid("truncated Story Media plan");
        if (buffer.readUnsignedByte() != 1) throw invalid("unsupported Story Media plan version");
        long revision = buffer.readLong();
        if (revision < 0) throw invalid("negative plan revision");
        int index = buffer.readUnsignedShort();
        int count = buffer.readUnsignedShort();
        validateChunk(index, count);
        int descriptorCount = buffer.readUnsignedByte();
        if (descriptorCount > MAX_DESCRIPTORS_PER_PACKET) throw invalid("too many package descriptors");
        List<Descriptor> parsed = new ArrayList<Descriptor>();
        for (int i = 0; i < descriptorCount; i++) parsed.add(Descriptor.read(buffer));
        if (!buffer.isReadable()) throw invalid("truncated running package IDs");
        if (buffer.readableBytes() < 2) throw invalid("truncated running package count");
        int runningCount = buffer.readUnsignedShort();
        if (runningCount > MAX_RUNNING_IDS) throw invalid("too many running package IDs");
        Set<String> running = new LinkedHashSet<String>();
        for (int i = 0; i < runningCount; i++) {
            String id = CanonicalSessionNetworkCodec.readField(buffer, "running_package_id", 96);
            if (!running.add(id)) throw invalid("duplicate running package ID");
        }
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        chunkIndex = index;
        chunkCount = count;
        this.revision = revision;
        descriptors = freezeDescriptors(parsed);
        runningPackageIds = freezeIds(running);
    }

    public static final class Descriptor {

        private final String packageId;
        private final String storyId;
        private final String version;
        private final String fingerprint;
        private final List<String> refs;
        private final boolean preload;

        public Descriptor(String packageId, String storyId, String version, String fingerprint, List<String> refs,
            boolean preload) {
            this.packageId = required(packageId, "package_id", 96);
            this.storyId = required(storyId, "story_id", 96);
            this.version = required(version, "package_version", 128);
            this.fingerprint = required(fingerprint, "content_fingerprint", MAX_FINGERPRINT_BYTES);
            if (refs == null || refs.size() > MAX_REFS_PER_PACKAGE) throw invalid("invalid media refs");
            LinkedHashSet<String> unique = new LinkedHashSet<String>();
            for (String ref : refs) {
                if (!CanonicalMediaReference.isValid(ref)) throw invalid("invalid media reference");
                if (!unique.add(ref)) throw invalid("duplicate media reference");
            }
            this.refs = Collections.unmodifiableList(new ArrayList<String>(unique));
            this.preload = preload;
        }

        public String getPackageId() {
            return packageId;
        }

        public String getStoryId() {
            return storyId;
        }

        public String getVersion() {
            return version;
        }

        public String getFingerprint() {
            return fingerprint;
        }

        public List<String> getRefs() {
            return refs;
        }

        public boolean isPreload() {
            return preload;
        }

        private void write(ByteBuf buffer) {
            CanonicalSessionNetworkCodec.writeField(buffer, packageId, "package_id", 96);
            CanonicalSessionNetworkCodec.writeField(buffer, storyId, "story_id", 96);
            CanonicalSessionNetworkCodec.writeField(buffer, version, "package_version", 128);
            CanonicalSessionNetworkCodec.writeField(buffer, fingerprint, "content_fingerprint", MAX_FINGERPRINT_BYTES);
            buffer.writeBoolean(preload);
            buffer.writeShort(refs.size());
            for (String ref : refs) CanonicalSessionNetworkCodec.writeField(buffer, ref, "media_ref", 80);
        }

        private static Descriptor read(ByteBuf buffer) {
            String packageId = CanonicalSessionNetworkCodec.readField(buffer, "package_id", 96);
            String storyId = CanonicalSessionNetworkCodec.readField(buffer, "story_id", 96);
            String version = CanonicalSessionNetworkCodec.readField(buffer, "package_version", 128);
            String fingerprint = CanonicalSessionNetworkCodec
                .readField(buffer, "content_fingerprint", MAX_FINGERPRINT_BYTES);
            if (!buffer.isReadable()) throw invalid("truncated preload flag");
            boolean preload = buffer.readBoolean();
            if (buffer.readableBytes() < 2) throw invalid("truncated media refs");
            int count = buffer.readUnsignedShort();
            if (count > MAX_REFS_PER_PACKAGE) throw invalid("too many media refs");
            List<String> refs = new ArrayList<String>();
            for (int i = 0; i < count; i++) refs.add(CanonicalSessionNetworkCodec.readField(buffer, "media_ref", 80));
            return new Descriptor(packageId, storyId, version, fingerprint, refs, preload);
        }
    }

    public static final class Handler implements IMessageHandler<StoryMediaPlan, IMessage> {

        @Override
        public IMessage onMessage(final StoryMediaPlan message, MessageContext context) {
            final Object connection = context.netHandler;
            darkgrey.rpg.network.MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    if (darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        darkgrey.rpg.media.CanonicalMediaClient.acceptPlan(message);
                }
            });
            return null;
        }
    }

    private static List<Descriptor> freezeDescriptors(List<Descriptor> values) {
        if (values == null || values.size() > MAX_PACKAGES) throw invalid("invalid package descriptors");
        LinkedHashSet<String> seen = new LinkedHashSet<String>();
        List<Descriptor> copy = new ArrayList<Descriptor>();
        for (Descriptor value : values) {
            if (value == null || !seen.add(value.packageId)) throw invalid("duplicate package descriptor");
            copy.add(value);
        }
        return Collections.unmodifiableList(copy);
    }

    private static Set<String> freezeIds(Set<String> values) {
        if (values == null || values.size() > MAX_RUNNING_IDS) throw invalid("invalid running package IDs");
        LinkedHashSet<String> copy = new LinkedHashSet<String>();
        for (String value : values) copy.add(required(value, "running_package_id", 96));
        return Collections.unmodifiableSet(copy);
    }

    private static String required(String value, String name, int bytes) {
        return CanonicalSessionNetworkCodec.requireField(value, name, bytes);
    }

    private static void validateChunk(int index, int count) {
        if (count < 1 || count > MAX_CHUNKS || index < 0 || index >= count) throw invalid("invalid plan chunk");
    }

    private static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid Story Media plan: " + message);
    }
}
