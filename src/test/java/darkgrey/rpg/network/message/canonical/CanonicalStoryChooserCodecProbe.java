package darkgrey.rpg.network.message.canonical;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Offline round-trip and malformed-payload checks for the chooser transport. */
public final class CanonicalStoryChooserCodecProbe {

    private CanonicalStoryChooserCodecProbe() {}

    public static void main(String[] args) {
        roundTrips();
        malformed();
        System.out.println("CANONICAL_STORY_CHOOSER_CODEC_PROBE=PASS");
    }

    private static void roundTrips() {
        CanonicalStoryChooserFrame frame = new CanonicalStoryChooserFrame(
            42L,
            Arrays.asList(
                new CanonicalStoryChooserFrame.Option("故事-继续", "继续的故事", "continue"),
                new CanonicalStoryChooserFrame.Option("story-start", "New story", "start"),
                new CanonicalStoryChooserFrame.Option("story-restart", "Old story", "restart"),
                new CanonicalStoryChooserFrame.Option("Team:Guard", "Guard", "start"),
                new CanonicalStoryChooserFrame.Option("Team:guard", "guard", "start")));
        CanonicalStoryChooserFrame decoded = roundTrip(frame);
        require(
            decoded.getToken() == 42L && decoded.getOptions()
                .size() == 5,
            "frame round-trip");
        require(
            "故事-继续".equals(
                decoded.getOptions()
                    .get(0)
                    .getStoryId()),
            "full story ID round-trip");
        require(
            "Team:Guard".equals(
                decoded.getOptions()
                    .get(3)
                    .getStoryId())
                && "Team:guard".equals(
                    decoded.getOptions()
                        .get(4)
                        .getStoryId()),
            "case-distinct namespaced story IDs round-trip");
        require(roundTrip(new CanonicalStoryChooserSelection(42L, -1)).getOptionIndex() == -1, "cancel round-trip");
        require(roundTrip(new CanonicalStoryChooserSelection(42L, 0)).getOptionIndex() == 0, "first option round-trip");
        ArrayList<CanonicalStoryChooserFrame.Option> many = new ArrayList<CanonicalStoryChooserFrame.Option>();
        for (int index = 0; index < 256; index++)
            many.add(new CanonicalStoryChooserFrame.Option("story-" + index, "Story " + index, "start"));
        require(
            roundTrip(new CanonicalStoryChooserFrame(1L, many)).getOptions()
                .size() == 256,
            "256 option bound");
    }

    private static void malformed() {
        reject(
            new CanonicalStoryChooserFrame(),
            Unpooled.buffer()
                .writeLong(1L)
                .writeShort(0),
            "frame missing data");
        reject(
            new CanonicalStoryChooserSelection(),
            Unpooled.buffer()
                .writeLong(1L)
                .writeShort(0),
            "truncated index");
        reject(
            new CanonicalStoryChooserSelection(),
            Unpooled.buffer()
                .writeLong(0L)
                .writeInt(-1),
            "nonpositive token");
        reject(
            new CanonicalStoryChooserSelection(),
            Unpooled.buffer()
                .writeLong(1L)
                .writeInt(256),
            "index too high");
        ByteBuf trailing = encode(new CanonicalStoryChooserSelection(1L, 0));
        trailing.writeByte(1);
        reject(new CanonicalStoryChooserSelection(), trailing, "selection trailing bytes");
        ByteBuf malformedUtf8 = Unpooled.buffer()
            .writeLong(1L)
            .writeShort(1)
            .writeShort(1)
            .writeByte(0xC3);
        reject(new CanonicalStoryChooserFrame(), malformedUtf8, "frame malformed UTF-8");
        ByteBuf unknownStatus = Unpooled.buffer()
            .writeLong(1L)
            .writeShort(1);
        write(unknownStatus, "story");
        write(unknownStatus, "Story");
        write(unknownStatus, "unknown");
        reject(new CanonicalStoryChooserFrame(), unknownStatus, "unknown status");
        ByteBuf frameTrailing = encode(
            new CanonicalStoryChooserFrame(
                1L,
                Collections.singletonList(new CanonicalStoryChooserFrame.Option("story", "Story", "start"))));
        frameTrailing.writeByte(1);
        reject(new CanonicalStoryChooserFrame(), frameTrailing, "frame trailing bytes");
    }

    private static void write(ByteBuf buffer, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        buffer.writeShort(bytes.length)
            .writeBytes(bytes);
    }

    private static ByteBuf encode(cpw.mods.fml.common.network.simpleimpl.IMessage message) {
        ByteBuf buffer = Unpooled.buffer();
        message.toBytes(buffer);
        return buffer;
    }

    @SuppressWarnings("unchecked")
    private static <T extends cpw.mods.fml.common.network.simpleimpl.IMessage> T roundTrip(T value) {
        try {
            T decoded = (T) value.getClass()
                .newInstance();
            decoded.fromBytes(encode(value));
            return decoded;
        } catch (Exception error) {
            throw new AssertionError(error);
        }
    }

    private static void reject(cpw.mods.fml.common.network.simpleimpl.IMessage target, ByteBuf bytes, String label) {
        try {
            target.fromBytes(bytes);
        } catch (RuntimeException expected) {
            return;
        }
        throw new AssertionError("Expected rejection: " + label);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
