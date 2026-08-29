package darkgrey.rpg.player;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

/**
 * UUID-isolated durable facts that complement the canonical Story/Session/Task
 * instance stores: important choices, persistent logic facts, and reward
 * receipts. The data always belongs to the overworld MapStorage so dimensions
 * never split player RPG state.
 */
public final class PlayerRpgSavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_players";
    private static final int SCHEMA_VERSION = 1;
    private final Map<UUID, Map<String, StoryFacts>> players = new LinkedHashMap<UUID, Map<String, StoryFacts>>();

    public PlayerRpgSavedData() {
        this(DATA_NAME);
    }

    public PlayerRpgSavedData(String name) {
        super(name);
    }

    public static PlayerRpgSavedData get() {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null) throw new IllegalStateException("Minecraft server is unavailable.");
        WorldServer overworld = server.worldServerForDimension(0);
        if (overworld == null) throw new IllegalStateException("Overworld is unavailable.");
        return get(overworld.mapStorage);
    }

    public static PlayerRpgSavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(PlayerRpgSavedData.class, DATA_NAME);
        if (loaded instanceof PlayerRpgSavedData) return (PlayerRpgSavedData) loaded;
        PlayerRpgSavedData created = new PlayerRpgSavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized String getFact(UUID playerUuid, String storyId, String key) {
        StoryFacts story = find(playerUuid, storyId);
        return story == null ? null : story.facts.get(requireKey(key, "Fact key"));
    }

    public synchronized boolean setFact(UUID playerUuid, String storyId, String key, String value) {
        if (value == null) throw new IllegalArgumentException("Fact value is required.");
        StoryFacts story = getOrCreate(playerUuid, storyId);
        String previous = story.facts.put(requireKey(key, "Fact key"), value);
        boolean changed = !value.equals(previous);
        if (changed) markDirty();
        return changed;
    }

    public synchronized String getChoice(UUID playerUuid, String storyId, String choiceId) {
        StoryFacts story = find(playerUuid, storyId);
        return story == null ? null : story.choices.get(requireKey(choiceId, "Choice ID"));
    }

    public synchronized boolean recordChoice(UUID playerUuid, String storyId, String choiceId, String optionId) {
        String option = requireKey(optionId, "Choice option ID");
        StoryFacts story = getOrCreate(playerUuid, storyId);
        String previous = story.choices.put(requireKey(choiceId, "Choice ID"), option);
        boolean changed = !option.equals(previous);
        if (changed) markDirty();
        return changed;
    }

    /** Returns true exactly once for one player/story/reward token. */
    public synchronized boolean claimReward(UUID playerUuid, String storyId, String rewardToken) {
        boolean added = getOrCreate(playerUuid, storyId).rewards.add(requireKey(rewardToken, "Reward token"));
        if (added) markDirty();
        return added;
    }

    public synchronized boolean hasClaimedReward(UUID playerUuid, String storyId, String rewardToken) {
        StoryFacts story = find(playerUuid, storyId);
        return story != null && story.rewards.contains(requireKey(rewardToken, "Reward token"));
    }

    /** Releases a provisional action receipt when the server-side effect failed before completion. */
    public synchronized boolean releaseRewardClaim(UUID playerUuid, String storyId, String rewardToken) {
        StoryFacts story = find(requirePlayer(playerUuid), requireKey(storyId, "Story ID"));
        boolean removed = story != null && story.rewards.remove(requireKey(rewardToken, "Reward token"));
        if (removed) markDirty();
        return removed;
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "players"), "Player RPG root");
        requireType(root, "schema_version", 3);
        if (root.getInteger("schema_version") != SCHEMA_VERSION)
            throw new IllegalArgumentException("Unsupported Player RPG schema_version.");
        NBTTagList playerValues = list(root, "players");
        Map<UUID, Map<String, StoryFacts>> candidate = new LinkedHashMap<UUID, Map<String, StoryFacts>>();
        for (int playerIndex = 0; playerIndex < playerValues.tagCount(); playerIndex++) {
            NBTTagCompound playerValue = playerValues.getCompoundTagAt(playerIndex);
            requireKeys(playerValue, set("player_uuid", "stories"), "Player RPG player");
            requireType(playerValue, "player_uuid", 8);
            UUID player;
            try {
                player = UUID.fromString(playerValue.getString("player_uuid"));
            } catch (RuntimeException exception) {
                throw new IllegalArgumentException("Invalid Player RPG UUID.", exception);
            }
            if (candidate.containsKey(player)) throw new IllegalArgumentException("Duplicate Player RPG UUID.");
            Map<String, StoryFacts> stories = new LinkedHashMap<String, StoryFacts>();
            NBTTagList storyValues = list(playerValue, "stories");
            for (int storyIndex = 0; storyIndex < storyValues.tagCount(); storyIndex++) {
                NBTTagCompound storyValue = storyValues.getCompoundTagAt(storyIndex);
                requireKeys(storyValue, set("story_id", "facts", "choices", "rewards"), "Player RPG story");
                String storyId = string(storyValue, "story_id");
                StoryFacts story = new StoryFacts();
                decodeMap(list(storyValue, "facts"), story.facts, "fact");
                decodeMap(list(storyValue, "choices"), story.choices, "choice");
                NBTTagList rewards = list(storyValue, "rewards", 8);
                for (int rewardIndex = 0; rewardIndex < rewards.tagCount(); rewardIndex++) {
                    String token = rewards.getStringTagAt(rewardIndex);
                    requireKey(token, "Reward token");
                    if (!story.rewards.add(token))
                        throw new IllegalArgumentException("Duplicate Player RPG reward token.");
                }
                if (stories.put(storyId, story) != null)
                    throw new IllegalArgumentException("Duplicate Player RPG Story ID.");
            }
            candidate.put(player, stories);
        }
        players.clear();
        players.putAll(candidate);
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        for (String key : new HashSet<String>(root.func_150296_c())) root.removeTag(key);
        root.setInteger("schema_version", SCHEMA_VERSION);
        NBTTagList playerValues = new NBTTagList();
        List<UUID> playerIds = new ArrayList<UUID>(players.keySet());
        Collections.sort(playerIds, new Comparator<UUID>() {

            @Override
            public int compare(UUID left, UUID right) {
                return left.toString()
                    .compareTo(right.toString());
            }
        });
        for (UUID playerId : playerIds) {
            NBTTagCompound playerValue = new NBTTagCompound();
            playerValue.setString("player_uuid", playerId.toString());
            NBTTagList storyValues = new NBTTagList();
            List<String> storyIds = new ArrayList<String>(
                players.get(playerId)
                    .keySet());
            Collections.sort(storyIds);
            for (String storyId : storyIds) {
                StoryFacts story = players.get(playerId)
                    .get(storyId);
                NBTTagCompound storyValue = new NBTTagCompound();
                storyValue.setString("story_id", storyId);
                storyValue.setTag("facts", encodeMap(story.facts));
                storyValue.setTag("choices", encodeMap(story.choices));
                NBTTagList rewards = new NBTTagList();
                List<String> tokens = new ArrayList<String>(story.rewards);
                Collections.sort(tokens);
                for (String token : tokens) rewards.appendTag(new net.minecraft.nbt.NBTTagString(token));
                storyValue.setTag("rewards", rewards);
                storyValues.appendTag(storyValue);
            }
            playerValue.setTag("stories", storyValues);
            playerValues.appendTag(playerValue);
        }
        root.setTag("players", playerValues);
    }

    private StoryFacts getOrCreate(UUID playerUuid, String storyId) {
        UUID player = requirePlayer(playerUuid);
        String story = requireKey(storyId, "Story ID");
        Map<String, StoryFacts> stories = players.get(player);
        if (stories == null) {
            stories = new LinkedHashMap<String, StoryFacts>();
            players.put(player, stories);
        }
        StoryFacts value = stories.get(story);
        if (value == null) {
            value = new StoryFacts();
            stories.put(story, value);
        }
        return value;
    }

    private StoryFacts find(UUID playerUuid, String storyId) {
        Map<String, StoryFacts> stories = players.get(requirePlayer(playerUuid));
        return stories == null ? null : stories.get(requireKey(storyId, "Story ID"));
    }

    private static NBTTagList encodeMap(Map<String, String> values) {
        NBTTagList result = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString("key", key);
            value.setString("value", values.get(key));
            result.appendTag(value);
        }
        return result;
    }

    private static void decodeMap(NBTTagList values, Map<String, String> target, String label) {
        for (int index = 0; index < values.tagCount(); index++) {
            NBTTagCompound value = values.getCompoundTagAt(index);
            requireKeys(value, set("key", "value"), "Player RPG " + label);
            String key = string(value, "key");
            requireType(value, "value", 8);
            if (target.put(key, value.getString("value")) != null)
                throw new IllegalArgumentException("Duplicate Player RPG " + label + " key.");
        }
    }

    private static NBTTagList list(NBTTagCompound value, String key) {
        return list(value, key, 10);
    }

    private static NBTTagList list(NBTTagCompound value, String key, int entryType) {
        requireType(value, key, 9);
        NBTTagList result = (NBTTagList) value.getTag(key);
        if (result.tagCount() > 0 && result.func_150303_d() != entryType)
            throw new IllegalArgumentException("Player RPG list '" + key + "' has wrong entry type.");
        return result;
    }

    private static String string(NBTTagCompound value, String key) {
        requireType(value, key, 8);
        return requireKey(value.getString(key), key);
    }

    private static UUID requirePlayer(UUID value) {
        if (value == null) throw new IllegalArgumentException("Player UUID is required.");
        return value;
    }

    private static String requireKey(String value, String label) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(label + " is required.");
        return value;
    }

    private static void requireType(NBTTagCompound value, String key, int type) {
        if (value == null || !value.hasKey(key, type))
            throw new IllegalArgumentException("Player RPG field '" + key + "' has wrong type or is missing.");
    }

    private static void requireKeys(NBTTagCompound value, Set<String> expected, String label) {
        if (value == null || !new HashSet<String>(value.func_150296_c()).equals(expected))
            throw new IllegalArgumentException(label + " has unknown or missing keys.");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static final class StoryFacts {

        private final Map<String, String> facts = new LinkedHashMap<String, String>();
        private final Map<String, String> choices = new LinkedHashMap<String, String>();
        private final Set<String> rewards = new HashSet<String>();
    }
}
