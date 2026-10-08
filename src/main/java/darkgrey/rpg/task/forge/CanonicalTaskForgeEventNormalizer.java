package darkgrey.rpg.task.forge;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;

import net.minecraft.entity.Entity;

import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Resolves concrete DGR identities; inventory collection is sampled separately. */
public final class CanonicalTaskForgeEventNormalizer {

    private CanonicalTaskForgeEventNormalizer() {}

    /** Credits only the actual lethal hit: direct real-player melee or owned projectile. */
    public static net.minecraft.entity.player.EntityPlayerMP creditedKiller(net.minecraft.util.DamageSource source) {
        if (source == null) return null;
        Entity owner = source.getEntity();
        if (!(owner instanceof net.minecraft.entity.player.EntityPlayerMP)
            || owner instanceof net.minecraftforge.common.util.FakePlayer) return null;
        Entity direct = source.getSourceOfDamage();
        boolean melee = direct == owner && "player".equals(source.getDamageType()) && !source.isProjectile();
        boolean projectile = source.isProjectile() && direct != null && direct != owner;
        return melee || projectile ? (net.minecraft.entity.player.EntityPlayerMP) owner : null;
    }

    public static List<CanonicalTaskEvent> killEvents(Entity entity) {
        return entity == null ? Collections.<CanonicalTaskEvent>emptyList()
            : killEventsForActors(EntityDgrIdentityResolver.resolveActorIds(entity));
    }

    /** One event per distinct, valid Actor address, in resolver order. */
    public static List<CanonicalTaskEvent> killEventsForActors(List<String> actorIds) {
        LinkedHashSet<String> targets = new LinkedHashSet<String>();
        if (actorIds != null) for (String id : actorIds) if (ResourceAddress.isKey(id) && ResourceAddress.fromKey(id)
            .getKind() == ResourceAddress.Kind.ACTOR) targets.add(id);
        List<CanonicalTaskEvent> events = new ArrayList<CanonicalTaskEvent>();
        for (String target : targets) events.add(CanonicalTaskEvent.killEntity(target));
        return Collections.unmodifiableList(events);
    }
}
