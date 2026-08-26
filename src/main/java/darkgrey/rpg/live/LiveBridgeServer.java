package darkgrey.rpg.live;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

import com.google.gson.Gson;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.Tags;

public final class LiveBridgeServer {

    public static final int PROTOCOL_VERSION = 1;
    private static final int MAX_LINE_LENGTH = 1024 * 1024;

    private final int port;
    private final LiveBridgeController controller;
    private final List<ClientConnection> clients = new CopyOnWriteArrayList<ClientConnection>();
    private volatile boolean running;
    private ServerSocket serverSocket;
    private Thread acceptThread;

    public LiveBridgeServer(int port, LiveBridgeController controller) {
        this.port = port;
        this.controller = controller;
    }

    public synchronized void start() throws IOException {
        if (running) {
            return;
        }
        serverSocket = new ServerSocket();
        serverSocket.setReuseAddress(true);
        serverSocket.bind(new InetSocketAddress(InetAddress.getByName("127.0.0.1"), port));
        running = true;
        acceptThread = new Thread(new Runnable() {

            @Override
            public void run() {
                acceptLoop();
            }
        }, "DarkGrey-RPG-Live-Accept");
        acceptThread.setDaemon(true);
        acceptThread.start();
        DarkGreyRpg.LOG.info("Live Bridge listening on 127.0.0.1:{}", port);
    }

    public synchronized void stop() {
        running = false;
        close(serverSocket);
        for (ClientConnection client : clients) {
            client.close();
        }
        clients.clear();
        DarkGreyRpg.LOG.info("Live Bridge stopped");
    }

    public void broadcast(JsonObject message) {
        for (ClientConnection client : clients) {
            client.send(message);
        }
    }

    private void acceptLoop() {
        while (running) {
            try {
                Socket socket = serverSocket.accept();
                if (!socket.getInetAddress()
                    .isLoopbackAddress()) {
                    socket.close();
                    continue;
                }
                ClientConnection client = new ClientConnection(socket);
                clients.add(client);
                client.start();
            } catch (IOException exception) {
                if (running) {
                    DarkGreyRpg.LOG.warn("Live Bridge accept failed", exception);
                }
            }
        }
    }

    private static void close(ServerSocket socket) {
        if (socket != null) {
            try {
                socket.close();
            } catch (IOException ignored) {}
        }
    }

    private final class ClientConnection implements LiveMessageSink {

        private final Socket socket;
        private final Gson gson = new Gson();
        private BufferedWriter writer;

        private ClientConnection(Socket socket) {
            this.socket = socket;
        }

        private void start() throws IOException {
            writer = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8));
            JsonObject hello = new JsonObject();
            hello.addProperty("type", "bridge.hello");
            hello.addProperty("protocol", PROTOCOL_VERSION);
            hello.addProperty("mod_version", Tags.VERSION);
            send(hello);
            Thread reader = new Thread(new Runnable() {

                @Override
                public void run() {
                    readLoop();
                }
            }, "DarkGrey-RPG-Live-Client");
            reader.setDaemon(true);
            reader.start();
        }

        private void readLoop() {
            try {
                BufferedReader reader = new BufferedReader(
                    new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
                String line;
                while (running && (line = reader.readLine()) != null) {
                    if (line.length() > MAX_LINE_LENGTH) {
                        throw new IOException("Live Bridge line exceeds " + MAX_LINE_LENGTH + " characters");
                    }
                    if (!line.trim()
                        .isEmpty()) {
                        controller.onMessage(
                            new JsonParser().parse(line)
                                .getAsJsonObject(),
                            this);
                    }
                }
            } catch (Exception exception) {
                if (running) {
                    DarkGreyRpg.LOG.debug("Live Bridge client disconnected: {}", exception.getMessage());
                }
            } finally {
                close();
            }
        }

        @Override
        public synchronized void send(JsonObject message) {
            try {
                writer.write(gson.toJson(message));
                writer.newLine();
                writer.flush();
            } catch (IOException exception) {
                close();
            }
        }

        private void close() {
            clients.remove(this);
            try {
                socket.close();
            } catch (IOException ignored) {}
        }
    }
}
