package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardOpenOption;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Opt-in N-layer duplicates through the real channel; never invokes a server handler or mutates a model. */
final class StabilityClient0402PacketFixture {

    private CanonicalSessionAction saved;
    private Object capturedConnection;
    private Object capturedWorld;
    private int delay, copies;

    void tick(File root, Object world, Object connection) throws Exception {
        if (world != capturedWorld || connection != capturedConnection) {
            if (copies > 0) record(root, "CANCELLED_CHANGED_CONNECTION", 0);
            copies = 0;
            delay = 0;
            saved = null;
            capturedWorld = null;
            capturedConnection = null;
        }
        if (copies > 0) {
            if (--delay <= 0) {
                for (int i = 0; i < copies; i++) DialogueNetwork.CHANNEL.sendToServer(saved);
                record(root, "SENT_REAL_CHANNEL", copies);
                copies = 0;
            }
        }
        File requestFile = new File(root, "packet-fixture-request.json");
        if (!requestFile.isFile()) return;
        JsonObject request = new JsonParser()
            .parse(new String(Files.readAllBytes(requestFile.toPath()), StandardCharsets.UTF_8))
            .getAsJsonObject();
        Files.delete(requestFile.toPath());
        require(world != null && connection != null, "Connected isolated client required");
        require(
            System.identityHashCode(connection) == request.get("connection_identity")
                .getAsInt(),
            "Owned connection fence");
        String action = request.get("action")
            .getAsString();
        if ("capture-choice".equals(action)) {
            require(copies == 0, "No pending replay");
            CanonicalSessionFrame frame = CanonicalSessionClientController.getFrame();
            require(
                frame != null && frame.getKind() == CanonicalSessionFrame.Kind.CHOICE,
                "Real CHOICE frame required");
            String option = request.get("option_id")
                .getAsString();
            boolean enabled = false;
            for (CanonicalSessionChoiceOption choice : frame.getChoices()) if (choice.getOptionId()
                .equals(option) && choice.isEnabled()) enabled = true;
            require(enabled, "Enabled authored option required");
            saved = new CanonicalSessionAction(
                frame.getTransportId(),
                frame.getStoryId(),
                frame.getCurrentNodeId(),
                CanonicalSessionAction.Kind.CHOICE,
                option,
                frame.getLineEpoch());
            capturedWorld = world;
            capturedConnection = connection;
            record(root, "CAPTURED_LEGAL_CHOICE", 0);
        } else if ("replay-saved".equals(action)) {
            require(saved != null && copies == 0, "Captured action and idle fixture required");
            require(world == capturedWorld && connection == capturedConnection, "Captured connection changed");
            copies = request.get("copies")
                .getAsInt();
            delay = request.get("delay_ticks")
                .getAsInt();
            require(copies >= 1 && copies <= 4 && delay >= 1 && delay <= 200, "Bounded test replay");
            record(root, "QUEUED_APPLICATION_DELAY", copies);
        } else throw new IllegalArgumentException("Unknown packet fixture action");
    }

    private void record(File root, String status, int count) throws Exception {
        JsonObject receipt = new JsonObject();
        receipt.addProperty("status", status);
        receipt.addProperty("sent_copies", count);
        receipt.addProperty("delay_client_ticks", delay);
        receipt.addProperty("transport", saved.getTransportId());
        receipt.addProperty("story", saved.getStoryId());
        receipt.addProperty("node", saved.getCurrentNodeId());
        receipt.addProperty("option", saved.getOptionId());
        receipt.addProperty("line_epoch", saved.getLineEpoch());
        receipt.addProperty("connection_identity", System.identityHashCode(capturedConnection));
        String fileName = System.getProperty("dgr0402.packetReceiptName", "packet-fixture-events");
        require(fileName.matches("[A-Za-z0-9-]+"), "Isolated packet receipt name");
        Files.write(
            new File(root, fileName + ".jsonl").toPath(),
            (receipt.toString() + "\n").getBytes(StandardCharsets.UTF_8),
            StandardOpenOption.CREATE,
            StandardOpenOption.APPEND);
    }

    private static void require(boolean accepted, String detail) {
        if (!accepted) throw new IllegalStateException(detail);
    }
}
