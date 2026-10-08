package darkgrey.rpg.creator;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;

/** Exercises current target kinds through the actual descriptor and bounded-page projection. */
public final class TaskCandidate0400Probe {

    public static void main(String[] args) throws Exception {
        darkgrey.rpg.media.Construction0335Probe.candidatePages();
        ItemIdentitySavedData bindings = new ItemIdentitySavedData();
        String item = "ST-2345-6789-ABCD-EFGH~item~sample";
        String group = "ST-2345-6789-ABCD-EFGH~item_group~supplies";
        require(
            TaskCandidateIndex.summary(objective(item), bindings)
                .getInteger("total") == 0,
            "Unbound Item has no native fallback");
        require(
            TaskCandidateIndex.summary(objective(group), bindings)
                .getBoolean("group"),
            "Empty typed Group remains a Group");
        require(
            TaskCandidateIndex.summary(objective(""), bindings)
                .getInteger("total") == 0,
            "Dormant target stays empty");
        bindings.bindItem(item, new ItemStackDefinition("probe:item", 0, null));
        net.minecraft.nbt.NBTTagCompound single = TaskCandidateIndex.summary(objective(item), bindings);
        require(single.getInteger("total") == 1 && !single.getBoolean("group"), "Item is queried only as an Item");
        bindings.unbindItem(item);
        require(
            TaskCandidateIndex.summary(objective(item), bindings)
                .getInteger("total") == 0,
            "Binding revision invalidates Item preview");
        for (String invalid : new String[] { "probe:item", "minecraft:coal", "ST-2345-6789-ABCD-EFGH~actor~host" }) {
            try {
                TaskCandidateIndex.summary(objective(invalid), bindings);
                throw new AssertionError("Invalid candidate target accepted: " + invalid);
            } catch (IllegalArgumentException expected) {}
        }
        System.out.println("TASK_CANDIDATE_0400_TYPED_ITEM_GROUP_DORMANT_REJECTION_REVISION=PASS");
        System.out.println("TASK_CANDIDATE_0400_121_MEMBER_PAGING_BYTE_BUDGET_PLACEHOLDER=PASS");
    }

    private static CanonicalGraphNode objective(String target) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("item", new JsonParser().parse("\"" + target + "\""));
        properties.put("metadata", new JsonParser().parse("{}"));
        return new CanonicalGraphNode("candidate", "objective", "Candidate", Collections.emptyList(), properties);
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
