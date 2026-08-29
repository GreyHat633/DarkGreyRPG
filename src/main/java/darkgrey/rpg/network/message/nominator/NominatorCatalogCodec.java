package darkgrey.rpg.network.message.nominator;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;

/** Strict bounded codec for server-produced nominator catalogs. */
public final class NominatorCatalogCodec {

    private NominatorCatalogCodec() {}

    public static void write(ByteBuf b, NominatorCatalog c) {
        writeCount(
            b,
            c.getStories()
                .size());
        for (NominatorCatalog.Story v : c.getStories()) {
            text(b, v.getId());
            text(b, v.getTitle());
            text(b, v.getNotes());
            tags(b, v.getTags());
        }
        writeCount(
            b,
            c.getActors()
                .size());
        for (NominatorCatalog.Actor v : c.getActors()) {
            text(b, v.getId());
            text(b, v.getDisplayName());
            text(b, v.getType());
            text(b, v.getStoryId());
            text(b, v.getNotes());
            tags(b, v.getTags());
        }
        writeItems(b, c.getItems());
        writeItems(b, c.getItemGroups());
    }

    public static NominatorCatalog read(ByteBuf b) {
        List<NominatorCatalog.Story> stories = new ArrayList<NominatorCatalog.Story>();
        for (int i = count(b); i-- > 0;) stories.add(new NominatorCatalog.Story(text(b), text(b), text(b), tags(b)));
        List<NominatorCatalog.Actor> actors = new ArrayList<NominatorCatalog.Actor>();
        for (int i = count(b); i-- > 0;)
            actors.add(new NominatorCatalog.Actor(text(b), text(b), text(b), text(b), text(b), tags(b)));
        return new NominatorCatalog(stories, actors, readItems(b), readItems(b));
    }

    private static void writeItems(ByteBuf b, List<NominatorCatalog.Item> values) {
        writeCount(b, values.size());
        for (NominatorCatalog.Item v : values) {
            text(b, v.getId());
            text(b, v.getDisplayName());
            tags(b, v.getTags());
        }
    }

    private static List<NominatorCatalog.Item> readItems(ByteBuf b) {
        List<NominatorCatalog.Item> values = new ArrayList<NominatorCatalog.Item>();
        for (int i = count(b); i-- > 0;) values.add(new NominatorCatalog.Item(text(b), text(b), tags(b)));
        return values;
    }

    private static void tags(ByteBuf b, List<String> values) {
        if (values.size() > NominatorCatalog.MAX_TAGS) throw new IllegalArgumentException("Too many catalog tags.");
        b.writeByte(values.size());
        for (String value : values) text(b, value);
    }

    private static List<String> tags(ByteBuf b) {
        int n = b.readUnsignedByte();
        if (n > NominatorCatalog.MAX_TAGS) throw new IllegalArgumentException("Too many catalog tags.");
        List<String> values = new ArrayList<String>();
        for (int i = 0; i < n; i++) values.add(text(b));
        return values;
    }

    private static void writeCount(ByteBuf b, int value) {
        if (value > NominatorCatalog.MAX_ENTRIES) throw new IllegalArgumentException("Catalog is too large.");
        b.writeShort(value);
    }

    private static int count(ByteBuf b) {
        int value = b.readUnsignedShort();
        if (value > NominatorCatalog.MAX_ENTRIES) throw new IllegalArgumentException("Catalog is too large.");
        return value;
    }

    private static void text(ByteBuf b, String value) {
        byte[] bytes = (value == null ? "" : value).getBytes(StandardCharsets.UTF_8);
        if (bytes.length > 256) throw new IllegalArgumentException("Catalog text is too long.");
        b.writeShort(bytes.length);
        b.writeBytes(bytes);
    }

    private static String text(ByteBuf b) {
        int n = b.readUnsignedShort();
        if (n > 256) throw new IllegalArgumentException("Catalog text is too long.");
        byte[] bytes = new byte[n];
        b.readBytes(bytes);
        return n == 0 ? null : new String(bytes, StandardCharsets.UTF_8);
    }
}
