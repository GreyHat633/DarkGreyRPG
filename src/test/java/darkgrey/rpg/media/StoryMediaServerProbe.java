package darkgrey.rpg.media;

import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.network.message.canonical.StoryMediaPlan;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Pure protocol and graph checks for the server Story media boundary. */
public final class StoryMediaServerProbe {

    private static final String REF = "media/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.png";

    private StoryMediaServerProbe() {}

    public static void main(String[] args) {
        List<CanonicalStoryLogicConnection> edges = Arrays.asList(
            new CanonicalStoryLogicConnection(
                "a",
                "flow_out",
                "b",
                "flow_in",
                darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW),
            new CanonicalStoryLogicConnection(
                "b",
                "logic_out",
                "c",
                "logic_in",
                darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.LOGIC),
            new CanonicalStoryLogicConnection(
                "c",
                "flow_out",
                "a",
                "flow_in",
                darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW));
        require(
            StoryMediaServer.downstreamStoryIds("a", edges)
                .equals(Arrays.asList("a", "b", "c")),
            "directed mixed Flow/Logic BFS cycle dedup");
        List<CanonicalStoryLogicConnection> branching = new java.util.ArrayList<CanonicalStoryLogicConnection>(edges);
        branching.add(
            new CanonicalStoryLogicConnection(
                "incoming",
                "out",
                "a",
                "in",
                darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.LOGIC));
        branching.add(
            new CanonicalStoryLogicConnection(
                "a",
                "out",
                "direct",
                "in",
                darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.LOGIC));
        require(
            StoryMediaServer.downstreamStoryIds("a", branching)
                .equals(Arrays.asList("a", "b", "direct", "c")),
            "ignore incoming links and prioritize direct neighbors before distant packages");
        List<CanonicalStoryLogicConnection> hundreds = new java.util.ArrayList<CanonicalStoryLogicConnection>();
        for (int i = 0; i < 500; i++) hundreds.add(
            new CanonicalStoryLogicConnection(
                "s" + i,
                "out",
                "s" + ((i + 1) % 500),
                "in",
                i % 2 == 0 ? darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW
                    : darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.LOGIC));
        require(
            StoryMediaServer.downstreamStoryIds("s0", hundreds)
                .size() == 500,
            "500-package mixed cycle terminates without dropping packages");
        StoryMediaPlan.Descriptor descriptor = new StoryMediaPlan.Descriptor(
            "pkg",
            "story",
            "1",
            "fingerprint",
            Collections.singletonList(REF),
            true);
        StoryMediaPlan source = new StoryMediaPlan(
            9L,
            0,
            1,
            Collections.singletonList(descriptor),
            Collections.singleton("pkg"));
        ByteBuf buffer = Unpooled.buffer();
        source.toBytes(buffer);
        StoryMediaPlan decoded = new StoryMediaPlan();
        decoded.fromBytes(buffer);
        require(
            decoded.getRevision() == 9L && decoded.getDescriptors()
                .size() == 1
                && decoded.getDescriptors()
                    .get(0)
                    .getRefs()
                    .contains(REF)
                && decoded.getRunningPackageIds()
                    .contains("pkg"),
            "plan wire round trip");
        require(
            decoded.getDescriptors()
                .get(0)
                .isPreload(),
            "real Story start preload flag");
        buffer.release();
        System.out.println("StoryMediaServerProbe PASS");
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
