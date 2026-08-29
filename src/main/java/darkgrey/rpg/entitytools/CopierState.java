package darkgrey.rpg.entitytools;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Multi-template state for one Copier ItemStack. */
public final class CopierState {

    public static final int SCHEMA_VERSION = 1;
    private final List<EntityTemplate> templates;
    private int selectedIndex;

    public CopierState() {
        this.templates = new ArrayList<EntityTemplate>();
        this.selectedIndex = -1;
    }

    public synchronized List<EntityTemplate> getTemplates() {
        return Collections.unmodifiableList(new ArrayList<EntityTemplate>(templates));
    }

    public synchronized int getSelectedIndex() {
        return selectedIndex;
    }

    public synchronized void select(int index) {
        if (index < 0 || index >= templates.size())
            throw new IllegalArgumentException("Copier template index is out of range.");
        selectedIndex = index;
    }

    public synchronized EntityTemplate remove(int index) {
        if (index < 0 || index >= templates.size())
            throw new IllegalArgumentException("Copier template index is out of range.");
        EntityTemplate removed = templates.remove(index);
        if (templates.isEmpty()) selectedIndex = -1;
        else if (selectedIndex > index) selectedIndex--;
        else if (selectedIndex >= templates.size()) selectedIndex = templates.size() - 1;
        return removed;
    }

    public synchronized EntityTemplate capture(EntityCapture capture) {
        EntityCapture.requireLivingMob(capture);
        EntityTemplate value = EntityTemplate.fromCapture(capture);
        EntityTemplateNbtCodec.decode(EntityTemplateNbtCodec.encode(value));
        templates.add(value);
        if (selectedIndex < 0) selectedIndex = 0;
        return value;
    }

    public synchronized EntitySpawnSpec copySelected(UUID freshUuid) {
        if (selectedIndex < 0 || selectedIndex >= templates.size())
            throw new IllegalStateException("Copier has no selected template.");
        if (freshUuid == null) throw new IllegalArgumentException("Fresh UUID is required.");
        return templates.get(selectedIndex)
            .spawn(freshUuid, null);
    }

    public synchronized NBTTagCompound writeToNBT() {
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setInteger("selected_index", selectedIndex);
        NBTTagList list = new NBTTagList();
        for (EntityTemplate template : templates) list.appendTag(EntityTemplateNbtCodec.encodeTemplate(template));
        root.setTag("templates", list);
        return root;
    }

    public synchronized void readFromNBT(NBTTagCompound root) {
        EntityTemplateNbtCodec.requireKeys(root, set("schema_version", "selected_index", "templates"), "copier");
        EntityTemplateNbtCodec.requireType(root, "schema_version", 3);
        EntityTemplateNbtCodec.requireType(root, "selected_index", 3);
        EntityTemplateNbtCodec.requireType(root, "templates", 9);
        if (root.getInteger("schema_version") != SCHEMA_VERSION)
            throw EntityTemplateNbtCodec.malformed("unsupported schema_version");
        int selected = root.getInteger("selected_index");
        NBTTagList list = root.getTagList("templates", 10);
        List<EntityTemplate> candidate = new ArrayList<EntityTemplate>();
        Set<String> fingerprints = new HashSet<String>();
        for (int i = 0; i < list.tagCount(); i++) {
            EntityTemplate value = EntityTemplateNbtCodec.decodeTemplate(list.getCompoundTagAt(i));
            String fingerprint = EntityTemplateNbtCodec.encode(value)
                .toString();
            if (!fingerprints.add(fingerprint)) throw EntityTemplateNbtCodec.malformed("duplicate copier template");
            candidate.add(value);
        }
        if (selected < -1 || selected >= candidate.size()) throw EntityTemplateNbtCodec.malformed("selected_index");
        templates.clear();
        templates.addAll(candidate);
        selectedIndex = selected;
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
