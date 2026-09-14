package darkgrey.rpg.creator;

import java.lang.reflect.Field;
import java.lang.reflect.Method;

import net.minecraft.entity.player.EntityPlayerMP;

import sun.misc.Unsafe;

/** Respawn keeps entityId but replaces the connection's player object. */
public final class RespawnPresentationCacheProbe {

    private RespawnPresentationCacheProbe() {}

    public static void verify() throws Exception {
        Field access = Unsafe.class.getDeclaredField("theUnsafe");
        access.setAccessible(true);
        Unsafe unsafe = (Unsafe) access.get(null);
        EntityPlayerMP original = (EntityPlayerMP) unsafe.allocateInstance(EntityPlayerMP.class);
        EntityPlayerMP replacement = (EntityPlayerMP) unsafe.allocateInstance(EntityPlayerMP.class);
        if (original == replacement || !original.equals(replacement)) throw new AssertionError("fixture identity");
        Method stateFor = CanonicalTaskPresentationServer.class.getDeclaredMethod("stateFor", EntityPlayerMP.class);
        stateFor.setAccessible(true);
        Object before = stateFor.invoke(null, original);
        Field projected = before.getClass()
            .getDeclaredField("sessionProjected");
        projected.setAccessible(true);
        projected.setBoolean(before, true);
        Object after = stateFor.invoke(null, replacement);
        if (before == after || projected.getBoolean(after)) throw new AssertionError("replacement must reproject");
        if (stateFor.invoke(null, replacement) != after) throw new AssertionError("same connection must reuse state");
    }
}
