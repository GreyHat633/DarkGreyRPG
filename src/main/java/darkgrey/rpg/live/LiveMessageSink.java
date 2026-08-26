package darkgrey.rpg.live;

import com.google.gson.JsonObject;

public interface LiveMessageSink {

    void send(JsonObject message);
}
