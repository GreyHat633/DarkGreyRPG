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
import darkgrey.rpg.client.gui.GuiCanonicalTaskSubmitChooser;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Server-authoritative choices for one physical actor-bound Task interaction. */
public final class CanonicalTaskSubmitChoiceFrame implements IMessage {

    public static final int MAX_OPTIONS = 256;
    private static final int MAX_TEXT_BYTES = 2048;
    private static final int PREVIEW_MAGIC = 0x44475249;
    private long token;
    private List<Option> options = Collections.emptyList();

    public CanonicalTaskSubmitChoiceFrame() {}

    public CanonicalTaskSubmitChoiceFrame(long token, List<Option> options) {
        validate(token, options);
        this.token = token;
        this.options = detached(options);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 10) throw invalid("truncated choice frame");
        long decodedToken = buffer.readLong();
        int count = buffer.readUnsignedShort();
        if (count > MAX_OPTIONS) throw invalid("too many choices");
        List<Option> decoded = new ArrayList<Option>(count);
        Set<String> identities = new HashSet<String>();
        for (int index = 0; index < count; index++) {
            String identity = read(buffer, "identity");
            String display = read(buffer, "display");
            if (!identities.add(identity)) throw invalid("duplicate choice");
            decoded.add(new Option(identity, display));
        }
        if (buffer.isReadable()) {
            if (buffer.readableBytes() < 8 || buffer.readInt() != PREVIEW_MAGIC) throw invalid("preview header");
            int length = buffer.readInt();
            if (length < 1 || length > 1048576 || length != buffer.readableBytes()) throw invalid("preview length");
            byte[] bytes = new byte[length];
            buffer.readBytes(bytes);
            try {
                net.minecraft.nbt.NBTTagCompound root = net.minecraft.nbt.CompressedStreamTools
                    .func_152457_a(bytes, new net.minecraft.nbt.NBTSizeTracker(2097152));
                net.minecraft.nbt.NBTTagList previews = root.getTagList("previews", 10);
                if (previews.tagCount() != count) throw invalid("preview count");
                for (int i = 0; i < count; i++) decoded
                    .set(i, new Option(decoded.get(i).identity, decoded.get(i).display, previews.getCompoundTagAt(i)));
            } catch (java.io.IOException e) {
                throw invalid("preview data");
            }
        }
        validate(decodedToken, decoded);
        token = decodedToken;
        options = detached(decoded);
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(token, options);
        buffer.writeLong(token);
        buffer.writeShort(options.size());
        for (Option option : options) {
            write(buffer, option.identity);
            write(buffer, option.display);
        }
        boolean hasPreview = false;
        for (Option option : options) hasPreview |= !option.preview.hasNoTags();
        if (hasPreview) {
            net.minecraft.nbt.NBTTagCompound root = new net.minecraft.nbt.NBTTagCompound();
            net.minecraft.nbt.NBTTagList previews = new net.minecraft.nbt.NBTTagList();
            for (Option option : options) previews.appendTag(option.getPreview());
            root.setTag("previews", previews);
            try {
                byte[] bytes = net.minecraft.nbt.CompressedStreamTools.compress(root);
                if (bytes.length > 1048576) throw invalid("preview too large");
                net.minecraft.nbt.CompressedStreamTools
                    .func_152457_a(bytes, new net.minecraft.nbt.NBTSizeTracker(2097152));
                buffer.writeInt(PREVIEW_MAGIC);
                buffer.writeInt(bytes.length);
                buffer.writeBytes(bytes);
            } catch (java.io.IOException e) {
                throw invalid("preview data");
            }
        }
    }

    public long getToken() {
        return token;
    }

    public List<Option> getOptions() {
        return options;
    }

    public static final class Handler implements IMessageHandler<CanonicalTaskSubmitChoiceFrame, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalTaskSubmitChoiceFrame message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    Minecraft.getMinecraft()
                        .displayGuiScreen(new GuiCanonicalTaskSubmitChooser(message));
                }
            });
            return null;
        }
    }

    public static final class Option {

        private final String identity;
        private final String display;
        private final net.minecraft.nbt.NBTTagCompound preview;

        public Option(String identity, String display) {
            this(identity, display, new net.minecraft.nbt.NBTTagCompound());
        }

        public Option(String identity, String display, net.minecraft.nbt.NBTTagCompound preview) {
            this.preview = (net.minecraft.nbt.NBTTagCompound) preview.copy();
            require(identity, "identity");
            require(display, "display");
            this.identity = identity;
            this.display = display;
        }

        public net.minecraft.nbt.NBTTagCompound getPreview() {
            return (net.minecraft.nbt.NBTTagCompound) preview.copy();
        }

        public String getIdentity() {
            return identity;
        }

        public String getDisplay() {
            return display;
        }
    }

    private static List<Option> detached(List<Option> values) {
        return Collections.unmodifiableList(new ArrayList<Option>(values));
    }

    private static void validate(long token, List<Option> values) {
        if (token <= 0 || values == null || values.isEmpty() || values.size() > MAX_OPTIONS)
            throw invalid("invalid choice frame");
        Set<String> identities = new HashSet<String>();
        for (Option option : values) {
            if (option == null || !identities.add(option.identity)) throw invalid("invalid choice option");
            require(option.identity, "identity");
            require(option.display, "display");
        }
    }

    private static String read(ByteBuf buffer, String name) {
        if (buffer.readableBytes() < 2) throw invalid("truncated " + name);
        int length = buffer.readUnsignedShort();
        if (length == 0 || length > MAX_TEXT_BYTES || length > buffer.readableBytes()) throw invalid("invalid " + name);
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        String value = new String(bytes, StandardCharsets.UTF_8);
        if (!java.util.Arrays.equals(bytes, value.getBytes(StandardCharsets.UTF_8))) throw invalid("invalid UTF-8");
        return value;
    }

    private static void write(ByteBuf buffer, String value) {
        byte[] bytes = require(value, "field").getBytes(StandardCharsets.UTF_8);
        buffer.writeShort(bytes.length);
        buffer.writeBytes(bytes);
    }

    private static String require(String value, String name) {
        if (value == null || value.trim()
            .isEmpty() || value.getBytes(StandardCharsets.UTF_8).length > MAX_TEXT_BYTES)
            throw invalid("invalid " + name);
        return value;
    }

    private static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid Task submit choice: " + message);
    }
}
