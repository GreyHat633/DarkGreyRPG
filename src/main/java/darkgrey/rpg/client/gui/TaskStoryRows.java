package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Stable story hierarchy over the already authorized player task projection. */
public final class TaskStoryRows {

    private TaskStoryRows() {}

    public static String key(NBTTagCompound task) {
        return task.getString("story");
    }

    public static List<NBTTagCompound> flatten(NBTTagList tasks, Set<String> expanded) {
        Map<String, List<NBTTagCompound>> groups = new LinkedHashMap<String, List<NBTTagCompound>>();
        Map<String, String> names = new LinkedHashMap<String, String>();
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            String key = key(task);
            if (!groups.containsKey(key)) groups.put(key, new ArrayList<NBTTagCompound>());
            groups.get(key)
                .add(task);
            String name = task.getString("story_title");
            names.put(key, name.isEmpty() ? (key.isEmpty() ? "未归属故事" : "故事 " + key) : name);
        }
        List<NBTTagCompound> rows = new ArrayList<NBTTagCompound>();
        for (Map.Entry<String, List<NBTTagCompound>> group : groups.entrySet()) {
            String name = names.get(group.getKey());
            int duplicates = 0;
            for (String other : names.values()) if (name.equals(other)) duplicates++;
            if (duplicates > 1) {
                String pack = group.getValue()
                    .get(0)
                    .getString("story_package");
                name += " · " + (pack.isEmpty() ? group.getKey() : pack);
            }
            NBTTagCompound header = new NBTTagCompound();
            header.setBoolean("story_header", true);
            header.setString("story", group.getKey());
            header.setString("title", name);
            rows.add(header);
            if (expanded.contains(group.getKey())) rows.addAll(group.getValue());
        }
        return rows;
    }
}
