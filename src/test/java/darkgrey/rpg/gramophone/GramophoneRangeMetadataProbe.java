package darkgrey.rpg.gramophone;

import net.minecraft.network.play.server.S35PacketUpdateTileEntity;

final class GramophoneRangeMetadataProbe {

    static void run() {
        TileGramophone server = new TileGramophone();
        TileGramophone client = new TileGramophone();
        server.radius = 2;
        server.revision = 7;
        server.source = "private-audio-source";
        S35PacketUpdateTileEntity packet = (S35PacketUpdateTileEntity) server.getDescriptionPacket();
        if (packet.func_148857_g()
            .hasKey("source")) throw new AssertionError("visual sync leaked audio source");
        client.onDataPacket(null, packet);
        if (!client.rangeMetadataReady || client.radius != 2
            || client.revision != 7
            || !client.instance.equals(server.instance)
            || !client.source.isEmpty()) throw new AssertionError("chunk visual metadata round trip");
        server.radius = 128;
        client.onDataPacket(null, (S35PacketUpdateTileEntity) server.getDescriptionPacket());
        if (client.radius != 128) throw new AssertionError("radius update outside audio index");
        server.radius = 129;
        client.onDataPacket(null, (S35PacketUpdateTileEntity) server.getDescriptionPacket());
        if (client.radius != 128) throw new AssertionError("invalid radius replaced valid state");
        System.out.println("GRAMOPHONE_RANGE_CHUNK_METADATA=PASS");
    }
}
