package darkgrey.rpg.story;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

public final class StoryNode {

    private final String id;
    private final StoryNodeType type;
    private final double x;
    private final double y;
    private final Map<String, String> properties;

    public StoryNode(String id, StoryNodeType type, double x, double y, Map<String, String> properties) {
        this.id = id;
        this.type = type;
        this.x = x;
        this.y = y;
        this.properties = Collections.unmodifiableMap(new LinkedHashMap<String, String>(properties));
    }

    public String getId() {
        return id;
    }

    public StoryNodeType getType() {
        return type;
    }

    public double getX() {
        return x;
    }

    public double getY() {
        return y;
    }

    public Map<String, String> getProperties() {
        return properties;
    }

    public String getProperty(String key) {
        return properties.get(key);
    }

    public int getIntProperty(String key) {
        return Integer.parseInt(properties.get(key));
    }

    public double getDoubleProperty(String key) {
        return Double.parseDouble(properties.get(key));
    }
}
