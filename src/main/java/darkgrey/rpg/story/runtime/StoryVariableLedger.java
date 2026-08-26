package darkgrey.rpg.story.runtime;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

public final class StoryVariableLedger {

    private final Map<String, Map<String, Map<String, String>>> players = new LinkedHashMap<String, Map<String, Map<String, String>>>();

    public String get(String playerId, String storyId, String variable) {
        Map<String, Map<String, String>> stories = players.get(playerId);
        if (stories == null || !stories.containsKey(storyId)) {
            return "";
        }
        String value = stories.get(storyId)
            .get(variable);
        return value == null ? "" : value;
    }

    public void set(String playerId, String storyId, String variable, String value) {
        Map<String, Map<String, String>> stories = players.get(playerId);
        if (stories == null) {
            stories = new LinkedHashMap<String, Map<String, String>>();
            players.put(playerId, stories);
        }
        Map<String, String> variables = stories.get(storyId);
        if (variables == null) {
            variables = new LinkedHashMap<String, String>();
            stories.put(storyId, variables);
        }
        variables.put(variable, value);
    }

    public Map<String, Map<String, String>> getStories(String playerId) {
        Map<String, Map<String, String>> stories = players.get(playerId);
        if (stories == null) {
            return Collections.emptyMap();
        }
        Map<String, Map<String, String>> copy = new LinkedHashMap<String, Map<String, String>>();
        for (Map.Entry<String, Map<String, String>> entry : stories.entrySet()) {
            copy.put(entry.getKey(), Collections.unmodifiableMap(new LinkedHashMap<String, String>(entry.getValue())));
        }
        return Collections.unmodifiableMap(copy);
    }

    public void clearStory(String playerId, String storyId) {
        Map<String, Map<String, String>> stories = players.get(playerId);
        if (stories != null) {
            stories.remove(storyId);
        }
    }

    public NBTTagCompound snapshotPlayer(String playerId) {
        NBTTagCompound root = new NBTTagCompound();
        NBTTagList storyTags = new NBTTagList();
        Map<String, Map<String, String>> stories = players.get(playerId);
        if (stories != null) {
            for (Map.Entry<String, Map<String, String>> storyEntry : stories.entrySet()) {
                NBTTagCompound storyTag = new NBTTagCompound();
                storyTag.setString("storyId", storyEntry.getKey());
                NBTTagList variableTags = new NBTTagList();
                for (Map.Entry<String, String> variable : storyEntry.getValue()
                    .entrySet()) {
                    NBTTagCompound variableTag = new NBTTagCompound();
                    variableTag.setString("key", variable.getKey());
                    variableTag.setString("value", variable.getValue());
                    variableTags.appendTag(variableTag);
                }
                storyTag.setTag("variables", variableTags);
                storyTags.appendTag(storyTag);
            }
        }
        root.setTag("stories", storyTags);
        return root;
    }

    public void restorePlayer(String playerId, NBTTagCompound root) {
        Map<String, Map<String, String>> restored = new LinkedHashMap<String, Map<String, String>>();
        NBTTagList storyTags = root.getTagList("stories", 10);
        for (int storyIndex = 0; storyIndex < storyTags.tagCount(); storyIndex++) {
            NBTTagCompound storyTag = storyTags.getCompoundTagAt(storyIndex);
            Map<String, String> variables = new LinkedHashMap<String, String>();
            NBTTagList variableTags = storyTag.getTagList("variables", 10);
            for (int variableIndex = 0; variableIndex < variableTags.tagCount(); variableIndex++) {
                NBTTagCompound variableTag = variableTags.getCompoundTagAt(variableIndex);
                variables.put(variableTag.getString("key"), variableTag.getString("value"));
            }
            restored.put(storyTag.getString("storyId"), variables);
        }
        players.put(playerId, restored);
    }

    public void writeToNbt(NBTTagCompound root) {
        NBTTagList playerTags = new NBTTagList();
        for (Map.Entry<String, Map<String, Map<String, String>>> playerEntry : players.entrySet()) {
            NBTTagCompound playerTag = new NBTTagCompound();
            playerTag.setString("playerUuid", playerEntry.getKey());
            NBTTagList storyTags = new NBTTagList();
            for (Map.Entry<String, Map<String, String>> storyEntry : playerEntry.getValue()
                .entrySet()) {
                NBTTagCompound storyTag = new NBTTagCompound();
                storyTag.setString("storyId", storyEntry.getKey());
                NBTTagList variableTags = new NBTTagList();
                for (Map.Entry<String, String> variable : storyEntry.getValue()
                    .entrySet()) {
                    NBTTagCompound variableTag = new NBTTagCompound();
                    variableTag.setString("key", variable.getKey());
                    variableTag.setString("value", variable.getValue());
                    variableTags.appendTag(variableTag);
                }
                storyTag.setTag("variables", variableTags);
                storyTags.appendTag(storyTag);
            }
            playerTag.setTag("stories", storyTags);
            playerTags.appendTag(playerTag);
        }
        root.setTag("players", playerTags);
    }

    public void readFromNbt(NBTTagCompound root) {
        players.clear();
        NBTTagList playerTags = root.getTagList("players", 10);
        for (int playerIndex = 0; playerIndex < playerTags.tagCount(); playerIndex++) {
            NBTTagCompound playerTag = playerTags.getCompoundTagAt(playerIndex);
            NBTTagList storyTags = playerTag.getTagList("stories", 10);
            for (int storyIndex = 0; storyIndex < storyTags.tagCount(); storyIndex++) {
                NBTTagCompound storyTag = storyTags.getCompoundTagAt(storyIndex);
                NBTTagList variableTags = storyTag.getTagList("variables", 10);
                for (int variableIndex = 0; variableIndex < variableTags.tagCount(); variableIndex++) {
                    NBTTagCompound variableTag = variableTags.getCompoundTagAt(variableIndex);
                    set(
                        playerTag.getString("playerUuid"),
                        storyTag.getString("storyId"),
                        variableTag.getString("key"),
                        variableTag.getString("value"));
                }
            }
        }
    }
}
