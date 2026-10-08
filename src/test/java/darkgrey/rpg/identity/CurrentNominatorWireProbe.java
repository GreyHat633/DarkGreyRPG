package darkgrey.rpg.identity;

import java.util.Collections;

import darkgrey.rpg.network.message.nominator.NominatorCatalogCodec;
import darkgrey.rpg.network.message.nominator.NominatorIdentityCodec;
import darkgrey.rpg.network.message.nominator.S2CNominatorEntityOpen;
import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class CurrentNominatorWireProbe {

    public static void main(String[] args) {
        String uid = "ST-2345-6789-ABCD-EFGH";
        for (ResourceAddress.Kind kind : ResourceAddress.Kind.values()) {
            String key = new ResourceAddress(StoryUid.parse(uid), kind, "shared").toKey();
            ByteBuf bytes = Unpooled.buffer();
            NominatorIdentityCodec.write(bytes, key, kind);
            if (!key.equals(NominatorIdentityCodec.read(bytes, kind)) || bytes.isReadable())
                throw new AssertionError("Address round trip");
            bytes.clear();
            NominatorIdentityCodec.write(bytes, key, kind);
            reject(
                () -> NominatorIdentityCodec.read(
                    bytes,
                    kind == ResourceAddress.Kind.ACTOR ? ResourceAddress.Kind.ITEM : ResourceAddress.Kind.ACTOR));
            reject(() -> NominatorIdentityCodec.write(Unpooled.buffer(), "Old:shared", kind));
            bytes.clear();
            bytes.writeByte(2);
            reject(() -> NominatorIdentityCodec.read(bytes, kind));
            bytes.release();
        }
        String actor = uid + "~actor~shared";
        NominatorCatalog catalog = new NominatorCatalog(
            Collections.singletonList(new NominatorCatalog.Story(uid, "Story", "", Collections.emptyList())),
            Collections.singletonList(
                new NominatorCatalog.Actor(actor, "Actor", "individual", uid, "", Collections.emptyList())),
            Collections.singletonList(new NominatorCatalog.Item(uid + "~item~shared", "Item", Collections.emptyList())),
            Collections.singletonList(
                new NominatorCatalog.Item(uid + "~item_group~shared", "Group", Collections.emptyList())));
        ByteBuf wire = Unpooled.buffer();
        NominatorCatalogCodec.write(wire, catalog);
        NominatorCatalog decoded = NominatorCatalogCodec.read(wire);
        if (!actor.equals(
            decoded.getActors()
                .get(0)
                .getId())
            || wire.isReadable()) throw new AssertionError("Catalog round trip");
        wire.clear();
        wire.writeInt(0);
        reject(() -> NominatorCatalogCodec.read(wire));
        wire.clear();
        wire.writeInt(0);
        reject(() -> new S2CNominatorEntityOpen().fromBytes(wire));
        wire.clear();
        java.util.UUID entity = java.util.UUID.randomUUID();
        S2CNominatorEntityOpen open = new S2CNominatorEntityOpen(
            7,
            entity,
            9,
            11,
            "Host",
            "mod:entity",
            actor,
            Collections.singletonList(actor),
            uid,
            catalog);
        open.toBytes(wire);
        S2CNominatorEntityOpen reopened = new S2CNominatorEntityOpen();
        reopened.fromBytes(wire);
        if (!entity.equals(reopened.getEntityUuid()) || reopened.getRevision() != 9
            || reopened.getCatalogRevision() != 11
            || !actor.equals(reopened.getIndividualId())
            || !Collections.singletonList(actor)
                .equals(reopened.getGroups())
            || wire.isReadable()) throw new AssertionError("Current entity Open round trip");
        wire.readerIndex(0);
        wire.setInt(0, 0x44475236);
        reject(() -> new S2CNominatorEntityOpen().fromBytes(wire));
        wire.readerIndex(0);
        wire.setInt(0, S2CNominatorEntityOpen.PROTOCOL_MARKER);
        wire.writeByte(1);
        reject(() -> new S2CNominatorEntityOpen().fromBytes(wire));
        wire.release();
        System.out.println(
            "CurrentNominatorWireProbe PASS: typed owner addresses, catalog round trip, legacy and kind rejection");
    }

    private static void reject(Runnable operation) {
        try {
            operation.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Invalid identity accepted");
    }
}
