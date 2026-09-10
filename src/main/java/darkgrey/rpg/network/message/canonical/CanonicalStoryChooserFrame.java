package darkgrey.rpg.network.message.canonical;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import net.minecraft.client.Minecraft;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.gui.GuiCanonicalStoryChooser;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Server-authoritative list of Story candidates awaiting an indexed selection. */
public final class CanonicalStoryChooserFrame implements IMessage {

    public static final int MAX_OPTIONS = 256;
    public static final int MAX_STORY_ID_BYTES = 96;
    public static final int MAX_DISPLAY_NAME_BYTES = 2048;
    public static final int MAX_STATUS_BYTES = 16;

    private long token;
    private List<Option> options = Collections.emptyList();

    public CanonicalStoryChooserFrame() {}

    public CanonicalStoryChooserFrame(long token, List<Option> options) {
        validate(token, options);
        this.token = token;
        this.options = detached(options);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 10) throw invalid("truncated chooser frame");
        long decodedToken = buffer.readLong();
        int count = buffer.readUnsignedShort();
        if (count > MAX_OPTIONS) throw invalid("too many chooser options");
        List<Option> decodedOptions = new ArrayList<Option>(count);
        Set<String> storyIds = new HashSet<String>();
        for (int index = 0; index < count; index++) {
            String storyId = readField(buffer, "story_id", MAX_STORY_ID_BYTES);
            String displayName = readField(buffer, "display_name", MAX_DISPLAY_NAME_BYTES);
            String status = readField(buffer, "status", MAX_STATUS_BYTES);
            if (!storyIds.add(storyId)) throw invalid("duplicate story_id: " + storyId);
            decodedOptions.add(new Option(storyId, displayName, status));
        }
        if (buffer.isReadable()) throw invalid("trailing unread bytes");
        validate(decodedToken, decodedOptions);
        token = decodedToken;
        options = detached(decodedOptions);
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(token, options);
        buffer.writeLong(token);
        buffer.writeShort(options.size());
        for (Option option : options) {
            writeField(buffer, option.getStoryId(), "story_id", MAX_STORY_ID_BYTES);
            writeField(buffer, option.getDisplayName(), "display_name", MAX_DISPLAY_NAME_BYTES);
            writeField(buffer, option.getStatus(), "status", MAX_STATUS_BYTES);
        }
    }

    public long getToken() {
        return token;
    }

    public List<Option> getOptions() {
        return options;
    }

    /** Marshals the chooser opening to the client main thread. */
    public static final class Handler implements IMessageHandler<CanonicalStoryChooserFrame, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalStoryChooserFrame message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    Minecraft.getMinecraft()
                        .displayGuiScreen(new GuiCanonicalStoryChooser(message));
                }
            });
            return null;
        }
    }

    public static final class Option {

        private final String storyId;
        private final String displayName;
        private final String status;

        public Option(String storyId, String displayName, String status) {
            requireField(storyId, "story_id", MAX_STORY_ID_BYTES);
            requireField(displayName, "display_name", MAX_DISPLAY_NAME_BYTES);
            requireStatus(status);
            this.storyId = storyId;
            this.displayName = displayName;
            this.status = status;
        }

        public String getStoryId() {
            return storyId;
        }

        public String getDisplayName() {
            return displayName;
        }

        public String getStatus() {
            return status;
        }
    }

    private static List<Option> detached(List<Option> values) {
        return Collections.unmodifiableList(new ArrayList<Option>(values));
    }

    private static void validate(long value, List<Option> values) {
        if (value <= 0) throw invalid("token must be positive");
        if (values == null || values.isEmpty() || values.size() > MAX_OPTIONS)
            throw invalid("invalid chooser option count");
        Set<String> storyIds = new HashSet<String>();
        for (Option option : values) {
            if (option == null) throw invalid("null chooser option");
            requireField(option.storyId, "story_id", MAX_STORY_ID_BYTES);
            requireField(option.displayName, "display_name", MAX_DISPLAY_NAME_BYTES);
            requireStatus(option.status);
            if (!storyIds.add(option.storyId)) throw invalid("duplicate story_id: " + option.storyId);
        }
    }

    private static void requireStatus(String status) {
        requireField(status, "status", MAX_STATUS_BYTES);
        if (!"continue".equals(status) && !"start".equals(status) && !"restart".equals(status))
            throw invalid("status must be continue, start, or restart");
    }

    private static String readField(ByteBuf buffer, String name, int maxBytes) {
        if (buffer.readableBytes() < 2) throw invalid("truncated " + name + " length");
        int length = buffer.readUnsignedShort();
        if (length == 0 || length > maxBytes || buffer.readableBytes() < length)
            throw invalid("invalid " + name + " length");
        byte[] encoded = new byte[length];
        buffer.readBytes(encoded);
        String value = new String(encoded, StandardCharsets.UTF_8);
        byte[] roundTrip = value.getBytes(StandardCharsets.UTF_8);
        if (!java.util.Arrays.equals(encoded, roundTrip)) throw invalid(name + " is not valid UTF-8");
        return value;
    }

    private static void writeField(ByteBuf buffer, String value, String name, int maxBytes) {
        byte[] encoded = requireField(value, name, maxBytes).getBytes(StandardCharsets.UTF_8);
        buffer.writeShort(encoded.length);
        buffer.writeBytes(encoded);
    }

    private static String requireField(String value, String name, int maxBytes) {
        if (value == null || value.trim()
            .isEmpty()) throw invalid(name + " must be non-blank");
        if (value.getBytes(StandardCharsets.UTF_8).length > maxBytes)
            throw invalid(name + " exceeds " + maxBytes + " UTF-8 bytes");
        return value;
    }

    private static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid canonical Story chooser payload: " + message);
    }
}
