package darkgrey.rpg.network.message.nominator;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;

/** Strict bounded codec for server-produced nominator catalogs. */
public final class NominatorCatalogCodec {

    private NominatorCatalogCodec() {}

    public static void write(ByteBuf b, NominatorCatalog c) {
        b.writeInt(0x44475237);
        writeCount(
            b,
            c.getStories()
                .size());
        for (NominatorCatalog.Story v : c.getStories()) {
            NominatorIdentityCodec.story(b, v.getId());
            text(b, v.getTitle());
            text(b, v.getNotes());
            tags(b, v.getTags());
        }
        writeCount(
            b,
            c.getActors()
                .size());
        for (NominatorCatalog.Actor v : c.getActors()) {
            NominatorIdentityCodec.write(b, v.getId(), ResourceAddress.Kind.ACTOR);
            text(b, v.getDisplayName());
            text(b, v.getType());
            NominatorIdentityCodec.story(b, v.getStoryId());
            text(b, v.getNotes());
            tags(b, v.getTags());
        }
        writeItems(b, c.getItems(), ResourceAddress.Kind.ITEM);
        writeItems(b, c.getItemGroups(), ResourceAddress.Kind.ITEM_GROUP);
        writeCount(
            b,
            c.getPackageChoices()
                .size());
        for (NominatorCatalog.PackageChoice v : c.getPackageChoices()) {
            text(b, v.getPackageId());
            NominatorIdentityCodec.story(b, v.getStoryId());
            text(b, v.getDisplayName());
            ids(b, v.getActorIds(), ResourceAddress.Kind.ACTOR);
            ids(b, v.getItemIds(), ResourceAddress.Kind.ITEM);
            ids(b, v.getItemGroupIds(), ResourceAddress.Kind.ITEM_GROUP);
            text(b, v.getContainerId());
            text(b, v.getContainerName());
            b.writeBoolean(v.isGroup());
            writeCount(
                b,
                v.getReferenceIds()
                    .size());
            for (String id : v.getReferenceIds()) text(b, id);
        }
    }

    public static NominatorCatalog read(ByteBuf b) {
        if (b.readInt() != 0x44475237) throw new IllegalArgumentException("Unsupported Nominator identity protocol");
        List<NominatorCatalog.Story> stories = new ArrayList<NominatorCatalog.Story>();
        for (int i = count(b); i-- > 0;)
            stories.add(new NominatorCatalog.Story(NominatorIdentityCodec.story(b), text(b), text(b), tags(b)));
        List<NominatorCatalog.Actor> actors = new ArrayList<NominatorCatalog.Actor>();
        for (int i = count(b); i-- > 0;) actors.add(
            new NominatorCatalog.Actor(
                NominatorIdentityCodec.read(b, ResourceAddress.Kind.ACTOR),
                text(b),
                text(b),
                NominatorIdentityCodec.story(b),
                text(b),
                tags(b)));
        List<NominatorCatalog.Item> items = readItems(b, ResourceAddress.Kind.ITEM);
        List<NominatorCatalog.Item> itemGroups = readItems(b, ResourceAddress.Kind.ITEM_GROUP);
        List<NominatorCatalog.PackageChoice> packages = new ArrayList<NominatorCatalog.PackageChoice>();
        for (int i = count(b); i-- > 0;) packages.add(
            new NominatorCatalog.PackageChoice(
                text(b),
                NominatorIdentityCodec.story(b),
                text(b),
                ids(b, ResourceAddress.Kind.ACTOR),
                ids(b, ResourceAddress.Kind.ITEM),
                ids(b, ResourceAddress.Kind.ITEM_GROUP),
                text(b),
                text(b),
                flag(b),
                referenceIds(b)));
        return new NominatorCatalog(stories, actors, items, itemGroups, packages);
    }

    private static boolean flag(ByteBuf b) {
        int value = b.readUnsignedByte();
        if (value > 1) throw new IllegalArgumentException("Invalid catalog directory flag.");
        return value == 1;
    }

    private static List<String> referenceIds(ByteBuf b) {
        List<String> ids = new ArrayList<String>();
        for (int n = count(b); n-- > 0;) {
            String id = text(b);
            ResourceAddress.fromKey(id);
            ids.add(id);
        }
        return ids;
    }

    private static void writeItems(ByteBuf b, List<NominatorCatalog.Item> values, ResourceAddress.Kind kind) {
        writeCount(b, values.size());
        for (NominatorCatalog.Item v : values) {
            NominatorIdentityCodec.write(b, v.getId(), kind);
            text(b, v.getDisplayName());
            tags(b, v.getTags());
        }
    }

    private static List<NominatorCatalog.Item> readItems(ByteBuf b, ResourceAddress.Kind kind) {
        List<NominatorCatalog.Item> values = new ArrayList<NominatorCatalog.Item>();
        for (int i = count(b); i-- > 0;)
            values.add(new NominatorCatalog.Item(NominatorIdentityCodec.read(b, kind), text(b), tags(b)));
        return values;
    }

    private static void ids(ByteBuf b, List<String> values, ResourceAddress.Kind kind) {
        writeCount(b, values.size());
        for (String value : values) NominatorIdentityCodec.write(b, value, kind);
    }

    private static List<String> ids(ByteBuf b, ResourceAddress.Kind kind) {
        int n = count(b);
        List<String> values = new ArrayList<String>();
        for (int i = 0; i < n; i++) values.add(NominatorIdentityCodec.read(b, kind));
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
        if (n == 0) return null;
        try {
            return StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(java.nio.charset.CodingErrorAction.REPORT)
                .onUnmappableCharacter(java.nio.charset.CodingErrorAction.REPORT)
                .decode(java.nio.ByteBuffer.wrap(bytes))
                .toString();
        } catch (java.nio.charset.CharacterCodingException invalid) {
            throw new IllegalArgumentException("Invalid catalog text encoding.", invalid);
        }
    }
}
