package darkgrey.rpg.task.runtime;

import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Strict Task reward package: ordered item/XP signed deltas only. */
public final class CanonicalTaskRewardPackage {

    private CanonicalTaskRewardPackage() {}

    public static final class Entry {

        private final String type;
        private final String item;
        private final int amount;

        private Entry(String type, String item, int amount) {
            this.type = type;
            this.item = item;
            this.amount = amount;
        }

        public String getType() {
            return type;
        }

        public String getItem() {
            return item;
        }

        public int getAmount() {
            return amount;
        }
    }

    public static List<Entry> read(CanonicalGraphNode node) {
        JsonElement entries = node.getProperties()
            .get("entries");
        if (node.getProperties()
            .size() != 1 || entries == null
            || !entries.isJsonArray()) throw invalid("Reward requires an entries array only.");
        List<Entry> result = new ArrayList<Entry>();
        for (JsonElement element : entries.getAsJsonArray()) {
            if (!element.isJsonObject()) throw invalid("Reward entry must be an object.");
            JsonObject entry = element.getAsJsonObject();
            String type = text(entry.get("type"));
            if (!"item".equals(type) && !"xp".equals(type)) throw invalid("Reward type must be item or xp.");
            if (entry.entrySet()
                .size() != ("item".equals(type) ? 3 : 2)) throw invalid("Unexpected reward fields.");
            String item = "item".equals(type) ? text(entry.get("item")) : null;
            JsonElement amount = entry.get("amount");
            if (amount == null || !amount.isJsonPrimitive()
                || !amount.getAsJsonPrimitive()
                    .isNumber())
                throw invalid("Reward amount must be a signed integer.");
            try {
                result.add(new Entry(type, item, new BigDecimal(amount.getAsString()).intValueExact()));
            } catch (ArithmeticException | NumberFormatException exception) {
                throw invalid("Reward amount must be a signed 32-bit integer.");
            }
        }
        return Collections.unmodifiableList(result);
    }

    private static String text(JsonElement value) {
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || value.getAsString()
                .trim()
                .isEmpty())
            throw invalid("Reward type/item must be nonblank text.");
        return value.getAsString();
    }

    private static CanonicalGraphResourceException invalid(String message) {
        return new CanonicalGraphResourceException("task.reward.package", message);
    }
}
