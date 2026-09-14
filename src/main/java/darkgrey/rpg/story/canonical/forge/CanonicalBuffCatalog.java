package darkgrey.rpg.story.canonical.forge;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import net.minecraft.potion.Potion;
import net.minecraft.util.StatCollector;

import cpw.mods.fml.common.Loader;
import cpw.mods.fml.common.ModContainer;

/** Cached 1.7.10 Potion lookup. Numeric IDs are diagnostics, never authoring identity. */
public final class CanonicalBuffCatalog {

    private static volatile CanonicalBuffCatalog current;
    private final Map<String, Potion> vanilla = new LinkedHashMap<String, Potion>();
    private final Map<String, List<Potion>> modded = new LinkedHashMap<String, List<Potion>>();
    private final List<Entry> entries = new ArrayList<Entry>();

    public static final class Entry {

        public final String modId, internalName, source;
        public final Potion potion;

        private Entry(String modId, String internalName, String source, Potion potion) {
            this.modId = modId;
            this.internalName = internalName;
            this.source = source;
            this.potion = potion;
        }

        public String displayLine() {
            return StatCollector.translateToLocal(
                potion.getName()) + " | " + internalName + " | " + modId + " | Potion ID=" + potion.id + " | " + source;
        }
    }

    private CanonicalBuffCatalog(Potion[] potions, List<ModContainer> mods) {
        String[] names = { "speed", "slowness", "haste", "mining_fatigue", "strength", "instant_health",
            "instant_damage", "jump_boost", "nausea", "regeneration", "resistance", "fire_resistance",
            "water_breathing", "invisibility", "blindness", "night_vision", "hunger", "weakness", "poison", "wither",
            "health_boost", "absorption", "saturation" };
        Potion[] effects = { Potion.moveSpeed, Potion.moveSlowdown, Potion.digSpeed, Potion.digSlowdown,
            Potion.damageBoost, Potion.heal, Potion.harm, Potion.jump, Potion.confusion, Potion.regeneration,
            Potion.resistance, Potion.fireResistance, Potion.waterBreathing, Potion.invisibility, Potion.blindness,
            Potion.nightVision, Potion.hunger, Potion.weakness, Potion.poison, Potion.wither, Potion.field_76434_w,
            Potion.field_76444_x, Potion.field_76443_y };
        for (int i = 0; i < names.length; i++) vanilla.put(names[i], effects[i]);
        for (Potion potion : potions) {
            if (potion == null) continue;
            boolean nativePotion = vanilla.containsValue(potion);
            Set<String> owners = new LinkedHashSet<String>();
            if (nativePotion) owners.add("minecraft");
            else for (ModContainer mod : mods) {
                List<String> packages = mod.getOwnedPackages();
                if (packages != null) for (String name : packages) if (potion.getClass()
                    .getName()
                    .startsWith(name.replace('/', '.') + ".")) owners.add(mod.getModId());
            }
            // Base Potion instances have no mod-owned class. Only an explicit
            // namespaced internal name can identify them; never pick an array neighbour.
            if (owners.isEmpty() && !nativePotion) for (ModContainer mod : mods) {
                String name = potion.getName()
                    .replaceFirst("^potion\\.", "");
                if (name.startsWith(mod.getModId() + ":") || name.startsWith(mod.getModId() + "."))
                    owners.add(mod.getModId());
            }
            String owner = owners.size() == 1 ? owners.iterator()
                .next() : "unresolved";
            String source = owners.size() == 1 ? (nativePotion ? "Vanilla"
                : potion.getClass()
                    .getName())
                : "unresolved owner " + owners;
            entries.add(new Entry(owner, potion.getName(), source, potion));
            if (!"unresolved".equals(owner)) {
                String key = key(owner, potion.getName());
                if (!modded.containsKey(key)) modded.put(key, new ArrayList<Potion>());
                modded.get(key)
                    .add(potion);
            }
        }
    }

    public static CanonicalBuffCatalog build(Potion[] potions, List<ModContainer> mods) {
        return new CanonicalBuffCatalog(potions, mods);
    }

    public static synchronized void rebuild() {
        current = build(
            Potion.potionTypes,
            Loader.instance()
                .getModList());
    }

    public static CanonicalBuffCatalog get() {
        if (current == null) rebuild();
        return current;
    }

    public List<Entry> entries() {
        return Collections.unmodifiableList(entries);
    }

    public Potion vanilla(String name) {
        Potion result = vanilla.get(name);
        if (result == null) throw new IllegalArgumentException("Unknown Vanilla buff: " + name);
        return result;
    }

    public Potion modded(String modId, String internalName) {
        List<Potion> matches = modded.get(key(modId, internalName));
        if (matches == null || matches.size() != 1) throw new IllegalArgumentException(
            "BUFF lookup requires exactly one match for (" + modId
                + ", "
                + internalName
                + "), found "
                + (matches == null ? 0 : matches.size())
                + ". Use /dgr buff export for diagnostics.");
        return matches.get(0);
    }

    private static String key(String mod, String name) {
        return mod.length() + ":" + mod + name;
    }

    /** Duration delta is author-facing seconds; level is author-facing 1-based. */
    public static int[] delta(int durationTicks, int level, int secondsDelta, int levelDelta) {
        long duration = (long) durationTicks + (long) secondsDelta * 20;
        long nextLevel = (long) level + levelDelta;
        if (duration <= 0 || nextLevel <= 0) return new int[] { 0, 0 };
        if (duration > Integer.MAX_VALUE || nextLevel > 128)
            throw new IllegalArgumentException("BUFF result exceeds 1.7.10 persistence limits (level 1..128).");
        return new int[] { (int) duration, (int) nextLevel };
    }
}
