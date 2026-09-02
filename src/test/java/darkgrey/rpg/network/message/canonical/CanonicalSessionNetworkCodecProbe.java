package darkgrey.rpg.network.message.canonical;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Bounded offline matrix for the canonical Session packet contract. */
public final class CanonicalSessionNetworkCodecProbe {

    private CanonicalSessionNetworkCodecProbe() {}

    public static void main(String[] args) {
        roundTrips();
        detachedCollections();
        rejectsMalformedPayloads();
        rejectsInvalidObjectsOnEncode();
        System.out.println("CANONICAL_SESSION_NETWORK_CODEC_PROBE=PASS");
    }

    private static void roundTrips() {
        CanonicalSessionAction continueAction = new CanonicalSessionAction(
            7L,
            "故事-1",
            "节点-甲",
            CanonicalSessionAction.Kind.CONTINUE,
            null);
        CanonicalSessionAction choiceAction = new CanonicalSessionAction(
            8L,
            "故事-1",
            "选择",
            CanonicalSessionAction.Kind.CHOICE,
            "option-右");
        CanonicalSessionFrame line = new CanonicalSessionFrame(
            7L,
            "故事-1",
            "session-资源",
            "节点-甲",
            CanonicalSessionFrame.Kind.LINE,
            "黛拉希娅",
            "你好，世界。",
            Collections.<CanonicalSessionChoiceOption>emptyList());
        CanonicalSessionFrame choice = new CanonicalSessionFrame(
            8L,
            "故事-1",
            "session-资源",
            "选择",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "请选择",
            Arrays.asList(
                new CanonicalSessionChoiceOption("option-左", "左边"),
                new CanonicalSessionChoiceOption("option-右", "右边")));
        CanonicalSessionFrame blankPromptChoice = new CanonicalSessionFrame(
            10L,
            "故事-1",
            "session-资源",
            "空提示选择",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "",
            Collections.singletonList(new CanonicalSessionChoiceOption("option-继续", "继续")));
        CanonicalSessionFrame narration = new CanonicalSessionFrame(
            9L,
            "故事-1",
            "session-资源",
            "旁白",
            CanonicalSessionFrame.Kind.NARRATION,
            "",
            "风穿过没有说话人的走廊。",
            Collections.<CanonicalSessionChoiceOption>emptyList());
        CanonicalSessionClose close = new CanonicalSessionClose(8L, "故事-1");
        require(roundTrip(continueAction).getKind() == CanonicalSessionAction.Kind.CONTINUE, "CONTINUE round-trip");
        require("option-右".equals(roundTrip(choiceAction).getOptionId()), "CHOICE option round-trip");
        CanonicalSessionFrame lineDecoded = roundTrip(line);
        require(
            lineDecoded.canContinue() && lineDecoded.getChoices()
                .isEmpty(),
            "LINE shape");
        CanonicalSessionFrame choiceDecoded = roundTrip(choice);
        require(
            !choiceDecoded.canContinue() && choiceDecoded.getChoices()
                .size() == 2,
            "CHOICE shape");
        require(
            roundTrip(blankPromptChoice).getText()
                .isEmpty(),
            "CHOICE blank prompt round-trip");
        CanonicalSessionFrame narrationDecoded = roundTrip(narration);
        require(
            narrationDecoded.getKind() == CanonicalSessionFrame.Kind.NARRATION && narrationDecoded.canContinue()
                && narrationDecoded.getSpeaker()
                    .isEmpty()
                && narrationDecoded.getChoices()
                    .isEmpty(),
            "NARRATION shape");
        require(
            "option-右".equals(
                choiceDecoded.getChoices()
                    .get(1)
                    .getOptionId()),
            "stable ordered option ID");
        require("故事-1".equals(roundTrip(close).getStoryId()), "close round-trip");
    }

    private static void detachedCollections() {
        List<CanonicalSessionChoiceOption> source = new ArrayList<CanonicalSessionChoiceOption>();
        source.add(new CanonicalSessionChoiceOption("stable", "Stable"));
        CanonicalSessionFrame frame = new CanonicalSessionFrame(
            1L,
            "story",
            "resource",
            "node",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "text",
            source);
        source.clear();
        require(
            frame.getChoices()
                .size() == 1,
            "constructor detached collection");
        reject(new Runnable() {

            @Override
            public void run() {
                frame.getChoices()
                    .clear();
            }
        }, "immutable collection");
        CanonicalSessionFrame decoded = roundTrip(frame);
        require(
            decoded.getChoices()
                .size() == 1,
            "decode detached collection");
        reject(new Runnable() {

            @Override
            public void run() {
                decoded.getChoices()
                    .add(new CanonicalSessionChoiceOption("x", "x"));
            }
        }, "decoded immutable collection");
    }

    private static void rejectsMalformedPayloads() {
        // Invalid enum, truncation, trailing bytes, nonpositive identity, blank/oversized UTF-8,
        // invalid action/frame shape, excessive choices, duplicate IDs, and malformed UTF-8.
        ByteBuf action = encodeRawAction(1L, "story", "node", 2, null);
        rejectDecode(action, new CanonicalSessionAction(), "invalid action enum");
        rejectDecode(
            Unpooled.buffer()
                .writeLong(1L),
            new CanonicalSessionAction(),
            "truncated action");
        ByteBuf trailing = encode(new CanonicalSessionClose(1L, "story"));
        trailing.writeByte(1);
        rejectDecode(trailing, new CanonicalSessionClose(), "close trailing bytes");
        rejectDecode(
            encodeRawAction(0L, "story", "node", 0, null),
            new CanonicalSessionAction(),
            "action nonpositive ID");
        rejectDecode(encodeRawAction(1L, " ", "node", 0, null), new CanonicalSessionAction(), "blank story");
        rejectDecode(encodeRawAction(1L, "story", "node", 1, ""), new CanonicalSessionAction(), "blank option");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 0, "speaker", "text", 1, new String[0]),
            new CanonicalSessionFrame(),
            "LINE with choice count");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 0, "", "text", 0, new String[0]),
            new CanonicalSessionFrame(),
            "LINE with blank speaker");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 0, "speaker", "", 0, new String[0]),
            new CanonicalSessionFrame(),
            "LINE with blank text");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 1, "", " ", 1, new String[] { "option" }),
            new CanonicalSessionFrame(),
            "CHOICE with whitespace prompt");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 1, "", "text", 0, new String[0]),
            new CanonicalSessionFrame(),
            "CHOICE with no choices");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 1, "speaker", "text", 1, new String[] { "same" }),
            new CanonicalSessionFrame(),
            "CHOICE with nonblank speaker");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 1, "", "text", 2, new String[] { "same", "same" }),
            new CanonicalSessionFrame(),
            "duplicate option IDs");
        rejectDecode(
            rawFrame(1L, "story", "resource", "node", 1, "", "text", 33, new String[0]),
            new CanonicalSessionFrame(),
            "excessive choices");
        ByteBuf malformedUtf8 = Unpooled.buffer();
        malformedUtf8.writeLong(1L)
            .writeShort(1)
            .writeByte(0xc3);
        rejectDecode(malformedUtf8, new CanonicalSessionClose(), "malformed UTF-8");
        ByteBuf oversized = Unpooled.buffer()
            .writeLong(1L)
            .writeShort(97)
            .writeBytes(repeatBytes(97));
        rejectDecode(oversized, new CanonicalSessionClose(), "oversized ID");
    }

    private static void rejectsInvalidObjectsOnEncode() {
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionAction(0L, "story", "node", CanonicalSessionAction.Kind.CONTINUE, null);
            }
        }, "invalid action object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionAction(1L, "story", "node", CanonicalSessionAction.Kind.CONTINUE, "forged");
            }
        }, "CONTINUE option object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionFrame(
                    1L,
                    "story",
                    "resource",
                    "node",
                    CanonicalSessionFrame.Kind.LINE,
                    "speaker",
                    "text",
                    Arrays.asList(new CanonicalSessionChoiceOption("id", "text")));
            }
        }, "LINE choice object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionFrame(
                    1L,
                    "story",
                    "resource",
                    "node",
                    CanonicalSessionFrame.Kind.CHOICE,
                    "",
                    "text",
                    Collections.<CanonicalSessionChoiceOption>emptyList());
            }
        }, "empty CHOICE object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionFrame(
                    1L,
                    "story",
                    "resource",
                    "node",
                    CanonicalSessionFrame.Kind.CHOICE,
                    "speaker",
                    "text",
                    Collections.singletonList(new CanonicalSessionChoiceOption("id", "text")));
            }
        }, "CHOICE nonblank speaker object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionClose(-1L, "story");
            }
        }, "invalid close object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionChoiceOption(" ", "text");
            }
        }, "blank option object");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionChoiceOption("id", " ");
            }
        }, "blank display object");
    }

    private static ByteBuf rawFrame(long transport, String story, String resource, String node, int kind,
        String speaker, String text, int count, String[] ids) {
        ByteBuf buffer = Unpooled.buffer();
        writeRaw(buffer, transport, story);
        writeRaw(buffer, resource);
        writeRaw(buffer, node);
        buffer.writeByte(kind);
        writeRaw(buffer, speaker);
        writeRaw(buffer, text);
        buffer.writeByte(count);
        for (String id : ids) {
            writeRaw(buffer, id);
            writeRaw(buffer, "display");
        }
        return buffer;
    }

    private static ByteBuf encodeRawAction(long transport, String story, String node, int kind, String option) {
        ByteBuf buffer = Unpooled.buffer();
        writeRaw(buffer, transport, story);
        writeRaw(buffer, node);
        buffer.writeByte(kind);
        if (kind == 1) writeRaw(buffer, option == null ? "option" : option);
        return buffer;
    }

    private static void writeRaw(ByteBuf buffer, long value, String field) {
        buffer.writeLong(value);
        writeRaw(buffer, field);
    }

    private static void writeRaw(ByteBuf buffer, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        buffer.writeShort(bytes.length)
            .writeBytes(bytes);
    }

    private static byte[] repeatBytes(int length) {
        byte[] result = new byte[length];
        Arrays.fill(result, (byte) 'x');
        return result;
    }

    private static ByteBuf encode(cpw.mods.fml.common.network.simpleimpl.IMessage message) {
        ByteBuf buffer = Unpooled.buffer();
        message.toBytes(buffer);
        return buffer;
    }

    @SuppressWarnings("unchecked")
    private static <T extends cpw.mods.fml.common.network.simpleimpl.IMessage> T roundTrip(T message) {
        T decoded;
        try {
            decoded = (T) message.getClass()
                .newInstance();
        } catch (Exception error) {
            throw new AssertionError(error);
        }
        decoded.fromBytes(encode(message));
        return decoded;
    }

    private static void rejectDecode(ByteBuf buffer, cpw.mods.fml.common.network.simpleimpl.IMessage target,
        String label) {
        reject(new Runnable() {

            @Override
            public void run() {
                target.fromBytes(buffer);
            }
        }, label);
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
        } catch (RuntimeException expected) {
            return;
        }
        throw new AssertionError("Expected rejection: " + label);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
