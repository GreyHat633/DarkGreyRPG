package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceNbtCodec;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation;
import darkgrey.rpg.session.runtime.CanonicalSessionRuntime;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class SessionPresentation0330Probe {

    private static final String AUDIO = "media/" + repeat('a') + ".ogg";
    private static final String IMAGE = "media/" + repeat('b') + ".png";

    private SessionPresentation0330Probe() {}

    public static void main(String[] args) {
        CanonicalGraphNode music = node(
            "music",
            "music",
            "{\"operation\":\"play\",\"media_ref\":\"" + AUDIO + "\",\"loop\":true,\"fade_in\":2,\"fade_out\":2}");
        CanonicalGraphNode screen = node(
            "screen",
            "screen",
            "{\"layers\":[{\"media_ref\":\"" + IMAGE
                + "\",\"x\":0.5,\"y\":0.5,\"width\":0.5,\"height\":0.7,\"anchor_x\":0.5,\"anchor_y\":0.5,\"z\":3}]}");
        CanonicalSessionPresentation state = CanonicalSessionPresentation.EMPTY.apply(music)
            .apply(screen);
        require(state.contains(AUDIO) && state.contains(IMAGE), "reachable media");
        require(state.getMusicRevision() == 1 && state.getRevision() == 2, "independent music revision");
        require(
            state.toJson()
                .equals(
                    CanonicalSessionPresentation.fromJson(state.toJson())
                        .toJson()),
            "detached snapshot round trip");
        CanonicalSessionPresentation empty = state.apply(node("clear", "screen", "{\"layers\":[]}"));
        require(
            empty.getLayers()
                .isEmpty() && empty.contains(AUDIO)
                && !empty.contains(IMAGE),
            "snapshot replacement clears removed images");
        reject(
            () -> CanonicalSessionPresentation.EMPTY.apply(
                node(
                    "bad",
                    "screen",
                    "{\"layers\":[{\"media_ref\":\"" + IMAGE
                        + "\",\"x\":0,\"y\":0,\"width\":0,\"height\":1,\"anchor_x\":0,\"anchor_y\":0,\"z\":0}]}")));
        reject(
            () -> CanonicalSessionPresentation.fromJson(
                state.toJson()
                    .replace("\"revision\":2", "\"revision\":-1")));
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("start", "start", "{}"),
            music,
            screen,
            node("line", "line", "{\"text\":\"测试台词\",\"voice_ref\":\"" + AUDIO + "\"}"),
            node("clear", "screen", "{\"layers\":[]}"),
            node("end", "end", "{\"port_id\":\"done\",\"display_name\":\"完成\"}"));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (int i = 0; i < nodes.size() - 1; i++) edges.add(
            new CanonicalGraphConnection(
                nodes.get(i)
                    .getId(),
                "flow_out",
                nodes.get(i + 1)
                    .getId(),
                "flow_in",
                CanonicalGraphInterfaceKind.FLOW));
        CanonicalGraphResource resource = new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            "probe",
            "Probe",
            new CanonicalGraph(nodes, edges));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(resource);
        require(
            runtime.snapshot()
                .getPresentation()
                .contains(IMAGE)
                && runtime.snapshot()
                    .getLineEpoch() == 1,
            "automatic media before line");
        CanonicalSessionInstanceSnapshot instance = new CanonicalSessionInstanceSnapshot(
            UUID.randomUUID(),
            "story",
            "placement",
            "probe",
            1,
            runtime.snapshot());
        CanonicalSessionInstanceSnapshot decoded = CanonicalSessionInstanceNbtCodec
            .decode(CanonicalSessionInstanceNbtCodec.encode(Collections.singletonList(instance)))
            .get(0);
        CanonicalSessionRuntime restored = CanonicalSessionRuntime.restore(resource, decoded.getRuntimeSnapshot());
        require(
            restored.snapshot()
                .getPresentation()
                .toJson()
                .equals(state.toJson()),
            "NBT restore presentation");
        restored.continueLine();
        require(
            restored.snapshot()
                .getPresentation()
                .getMusicRef() == null && restored.snapshot()
                    .getPresentation()
                    .getLayers()
                    .isEmpty(),
            "END clears all presentation");
        CanonicalSessionFrame frame = frame(state, 1, true);
        ByteBuf buffer = Unpooled.buffer();
        frame.toBytes(buffer);
        CanonicalSessionFrame wire = new CanonicalSessionFrame();
        wire.fromBytes(buffer);
        buffer.release();
        require(
            wire.shouldPlayVoice() && wire.getLineEpoch() == 1
                && wire.getPresentation()
                    .contains(IMAGE),
            "bounded wire round trip");
        Fake backend = new Fake();
        SessionAudioPlayback audio = new SessionAudioPlayback(backend);
        audio.present(frame, 0);
        require(backend.plays == 0, "late media never blocks");
        audio.advance();
        backend.available = true;
        audio.tick(1, true);
        require(
            backend.plays == 1 && !backend.playing.containsKey("dgr_session_voice"),
            "late advanced voice never starts");
        audio.present(frame(state, 2, true), 2);
        require(backend.plays == 2, "new line plays once");
        audio.tick(3, true);
        require(backend.plays == 2, "voice ending does not replay");
        audio.tick(4, false);
        require(backend.playing.isEmpty(), "death stops DGR channels");
        audio.tick(5, true);
        require(backend.plays == 3 && !backend.playing.containsKey("dgr_session_voice"), "respawn restores music only");
        Fake failed = new Fake();
        failed.available = true;
        failed.failVoice = true;
        SessionAudioPlayback failedAudio = new SessionAudioPlayback(failed);
        failedAudio.present(frame(CanonicalSessionPresentation.EMPTY, 3, true), 0);
        failedAudio.tick(1, true);
        failedAudio.tick(2, true);
        require(failed.plays == 1, "failed voice is attempted once per line");
        failedAudio.advance();
        require(failed.playing.isEmpty(), "advance stops failed voice channel");
        audio.clear();
        audio.present(frame(state, 2, false), 6);
        require(backend.plays == 4 && !backend.playing.containsKey("dgr_session_voice"), "reconnect suppresses voice");
        audio.present(frame(state.apply(music), 2, false), 7);
        require(backend.playing.size() == 2, "music replacement crossfade");
        audio.tick(10, true);
        require(backend.playing.size() == 1, "fade releases previous channel");
        audio.clear();
        require(backend.playing.isEmpty(), "END cleanup");
        verifyVolumes(music);
        CanonicalSessionFrame portraitLine = new CanonicalSessionFrame(
            1,
            "story",
            "probe",
            "line",
            CanonicalSessionFrame.Kind.LINE,
            "speaker",
            "line",
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            IMAGE,
            AUDIO);
        CanonicalSessionFrame choice = new CanonicalSessionFrame(
            1,
            "story",
            "probe",
            "choice",
            CanonicalSessionFrame.Kind.CHOICE,
            "",
            "",
            Collections.singletonList(new CanonicalSessionChoiceOption("a", "A")));
        require(
            IMAGE.equals(CanonicalMediaServer.retainedPortrait(portraitLine, choice, IMAGE)),
            "choice keeps in-flight portrait download authorized");
        require(
            CanonicalMediaServer.retainedPortrait(portraitLine, frame(state, 2, true), IMAGE) == null,
            "next line without portrait revokes previous image");
        System.out.println("SESSION_PRESENTATION_0330_PROBE=PASS");
    }

    private static void verifyVolumes(CanonicalGraphNode originalMusic) {
        CanonicalSessionPresentation low = CanonicalSessionPresentation.EMPTY.apply(
            node(
                "low",
                "music",
                "{\"operation\":\"play\",\"media_ref\":\"" + AUDIO
                    + "\",\"loop\":true,\"fade_in\":0,\"fade_out\":2,\"volume\":0.25}"));
        require(
            CanonicalSessionPresentation.fromJson(low.toJson())
                .getMusicVolume() == 0.25,
            "music volume persists in presentation");
        require(
            CanonicalSessionPresentation.fromJson(
                low.toJson()
                    .replace(",\"volume\":0.25", ""))
                .getMusicVolume() == 1,
            "old snapshots default to full volume");
        CanonicalSessionFrame quiet = new CanonicalSessionFrame(
            1,
            "story",
            "probe",
            "line",
            CanonicalSessionFrame.Kind.LINE,
            "",
            "volume",
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            null,
            AUDIO,
            0.4).withPresentation(low, 1, true);
        ByteBuf bytes = Unpooled.buffer();
        quiet.toBytes(bytes);
        CanonicalSessionFrame decoded = new CanonicalSessionFrame();
        decoded.fromBytes(bytes);
        bytes.readerIndex(0);
        bytes.writerIndex(bytes.writerIndex() - 16);
        CanonicalSessionFrame legacy = new CanonicalSessionFrame();
        legacy.fromBytes(bytes);
        require(legacy.getVoiceVolume() == 1, "older network frames retain default gain");
        bytes.release();
        require(decoded.getVoiceVolume() == 0.4, "voice volume wire round trip");
        Fake fake = new Fake();
        fake.available = true;
        SessionAudioPlayback playback = new SessionAudioPlayback(fake);
        playback.present(decoded, 0);
        require(Math.abs(fake.volumes.get("dgr_session_voice") - 0.4) < 0.0001, "voice gain applied");
        require(fake.volumes.get("dgr_session_music_a") == 0.25f, "music gain applied");
        playback.present(frame(low.apply(originalMusic), 2, false), 1);
        playback.tick(2, true);
        require(
            fake.volumes.get("dgr_session_music_a") == 0.125f,
            "crossfade starts from configured gain, never jumps to full volume");
        reject(
            () -> CanonicalSessionPresentation.fromJson(
                low.toJson()
                    .replace("\"volume\":0.25", "\"volume\":2")));
        playback.clear();
    }

    private static CanonicalSessionFrame frame(CanonicalSessionPresentation state, long epoch, boolean voice) {
        return new CanonicalSessionFrame(
            1,
            "story",
            "probe",
            "line",
            CanonicalSessionFrame.Kind.LINE,
            "",
            "测试",
            Collections.<CanonicalSessionChoiceOption>emptyList(),
            null,
            AUDIO).withPresentation(state, epoch, voice);
    }

    private static CanonicalGraphNode node(String id, String type, String json) {
        List<CanonicalGraphPort> ports = new ArrayList<CanonicalGraphPort>();
        if (!"start".equals(type)) ports.add(
            new CanonicalGraphPort(
                "flow_in",
                "In",
                CanonicalGraphPortDirection.INPUT,
                CanonicalGraphInterfaceKind.FLOW,
                0));
        if (!"end".equals(type)) ports.add(
            new CanonicalGraphPort(
                "flow_out",
                "Out",
                CanonicalGraphPortDirection.OUTPUT,
                CanonicalGraphInterfaceKind.FLOW,
                1));
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : new JsonParser().parse(json)
            .getAsJsonObject()
            .entrySet()) properties.put(entry.getKey(), entry.getValue());
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static String repeat(char value) {
        char[] chars = new char[64];
        Arrays.fill(chars, value);
        return new String(chars);
    }

    private static void reject(Runnable action) {
        try {
            action.run();
            throw new AssertionError("expected rejection");
        } catch (IllegalArgumentException expected) {}
    }

    private static void require(boolean value, String reason) {
        if (!value) throw new AssertionError(reason);
    }

    private static final class Fake implements SessionAudioPlayback.Backend {

        boolean available;
        boolean failVoice;
        int plays;
        final Map<String, String> playing = new HashMap<String, String>();
        final Map<String, Float> volumes = new HashMap<String, Float>();

        @Override
        public boolean ready(String ref) {
            return available;
        }

        @Override
        public boolean play(String channel, String ref, boolean loop) {
            playing.put(channel, ref);
            plays++;
            return !failVoice || !"dgr_session_voice".equals(channel);
        }

        @Override
        public void volume(String channel, float volume) {
            require(volume >= 0 && volume <= 1, "volume bounds");
            volumes.put(channel, volume);
        }

        @Override
        public void stop(String channel) {
            playing.remove(channel);
        }
    }
}
