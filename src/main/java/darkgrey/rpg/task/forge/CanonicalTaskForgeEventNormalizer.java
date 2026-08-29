package darkgrey.rpg.task.forge;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityList;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Forge-to-canonical value normalization. This class has no dispatch side effects. */
public final class CanonicalTaskForgeEventNormalizer {

    private static final Set<String> VANILLA_LIVING_IDS = Collections.unmodifiableSet(
        new HashSet<String>(
            Arrays.asList(
                "bat",
                "blaze",
                "cavespider",
                "chicken",
                "cow",
                "creeper",
                "enderdragon",
                "enderman",
                "entityhorse",
                "ghast",
                "giant",
                "lavaslime",
                "mushroomcow",
                "ozelot",
                "pig",
                "pigzombie",
                "sheep",
                "silverfish",
                "skeleton",
                "slime",
                "snowman",
                "spider",
                "squid",
                "villager",
                "villagergolem",
                "witch",
                "witherboss",
                "wolf",
                "zombie")));

    private CanonicalTaskForgeEventNormalizer() {}

    public static CanonicalTaskEvent kill(Entity entity) {
        String id = normalizeEntityId(entity);
        return id == null ? null : CanonicalTaskEvent.killEntity(id);
    }

    public static CanonicalTaskEvent killEvent(Entity entity) {
        return kill(entity);
    }

    public static String normalizeEntityId(Entity entity) {
        if (entity == null) return null;
        String id = EntityList.getEntityString(entity);
        if (id == null || id.trim()
            .isEmpty()) return null;
        return normalizeEntityId(id);
    }

    /** Normalizes Forge's legacy vanilla names while preserving namespaced mod IDs. */
    public static String normalizeEntityId(String raw) {
        if (raw == null) return null;
        String value = raw.trim();
        if (value.isEmpty()) return null;
        String lower = value.toLowerCase(Locale.ROOT);
        if (lower.indexOf(':') >= 0) return lower;
        if (!VANILLA_LIVING_IDS.contains(lower)) return null;
        if ("lavaslime".equals(lower)) return "minecraft:magma_cube";
        if ("mushroomcow".equals(lower)) return "minecraft:mooshroom";
        if ("ozelot".equals(lower)) return "minecraft:ocelot";
        if ("pigzombie".equals(lower)) return "minecraft:zombie_pigman";
        if ("snowman".equals(lower)) return "minecraft:snowman";
        if ("villagergolem".equals(lower)) return "minecraft:iron_golem";
        if ("witherboss".equals(lower)) return "minecraft:wither";
        if ("cavespider".equals(lower)) return "minecraft:cave_spider";
        if ("enderdragon".equals(lower)) return "minecraft:ender_dragon";
        if ("entityhorse".equals(lower)) return "minecraft:horse";
        StringBuilder snake = new StringBuilder();
        for (int i = 0; i < lower.length(); i++) {
            char character = lower.charAt(i);
            if (i > 0 && Character.isUpperCase(value.charAt(i)) && Character.isLowerCase(value.charAt(i - 1)))
                snake.append('_');
            snake.append(character);
        }
        return "minecraft:" + snake.toString();
    }

    public static CanonicalTaskEvent collect(ItemStack stack) {
        if (stack == null || stack.getItem() == null || stack.stackSize <= 0) return null;
        Object registryName = Item.itemRegistry.getNameForObject(stack.getItem());
        if (registryName == null) return null;
        return collect(String.valueOf(registryName), stack.getItemDamage(), stack.stackSize);
    }

    public static CanonicalTaskEvent collectEvent(ItemStack stack) {
        return collect(stack);
    }

    /** Testable collect seam matching the ItemStack mapping contract. */
    public static CanonicalTaskEvent collect(String registryName, int itemDamage, int amount) {
        if (registryName == null || amount <= 0 || itemDamage < 0) return null;
        String item = registryName.trim()
            .toLowerCase(Locale.ROOT);
        if (item.isEmpty() || item.indexOf(':') <= 0 || item.indexOf(':') == item.length() - 1) return null;
        return CanonicalTaskEvent
            .collectItem(item, java.util.Collections.singletonMap("damage", String.valueOf(itemDamage)), amount);
    }

    public static CanonicalTaskEvent interact(Entity target) {
        if (target == null) return null;
        String actorId = EntityDgrIdentityResolver.resolveActorId(target);
        return actorId == null || actorId.trim()
            .isEmpty() ? null : CanonicalTaskEvent.interactActor(actorId.trim());
    }

    /** Normalizes every resolved identity in stable order for one Forge interaction. */
    public static List<CanonicalTaskEvent> interactEvents(Entity target) {
        if (target == null) return Collections.emptyList();
        return interactEventsForIds(EntityDgrIdentityResolver.resolveActorIds(target));
    }

    /** Testable multi-identity seam; duplicate and blank IDs are ignored. */
    public static List<CanonicalTaskEvent> interactEventsForIds(List<String> actorIds) {
        if (actorIds == null || actorIds.isEmpty()) return Collections.emptyList();
        LinkedHashSet<String> unique = new LinkedHashSet<String>();
        for (String actorId : actorIds) if (actorId != null && !actorId.trim()
            .isEmpty()) unique.add(actorId.trim());
        if (unique.isEmpty()) return Collections.emptyList();
        List<CanonicalTaskEvent> events = new ArrayList<CanonicalTaskEvent>();
        for (String actorId : unique) events.add(CanonicalTaskEvent.interactActor(actorId));
        return Collections.unmodifiableList(events);
    }

    public static List<CanonicalTaskEvent> interactAll(Entity target) {
        return interactEvents(target);
    }

    public static CanonicalTaskEvent interactEvent(Entity target) {
        return interact(target);
    }

    /** Testable actor-id seam; only nonblank IDs become canonical events. */
    public static CanonicalTaskEvent interact(String actorId) {
        if (actorId == null || actorId.trim()
            .isEmpty()) return null;
        return CanonicalTaskEvent.interactActor(actorId.trim());
    }
}
