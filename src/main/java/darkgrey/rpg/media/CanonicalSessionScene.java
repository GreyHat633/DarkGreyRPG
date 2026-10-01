package darkgrey.rpg.media;

import java.util.Collections;
import java.util.HashSet;
import java.util.Set;

import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.Tessellator;
import net.minecraft.util.ResourceLocation;

import org.lwjgl.opengl.GL11;

import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Temporary screen performance only. No changes to story progression or media admission. */
public final class CanonicalSessionScene {

    private static final ScreenTransition animation = new ScreenTransition();
    private static CanonicalSessionFrame pending;
    private static long transport = -1, revision = -1, prepareStarted;
    private static Set<String> demanded = Collections.emptySet(), pinned = new HashSet<String>();

    private CanonicalSessionScene() {}

    public static void present(CanonicalSessionFrame frame) {
        if (transport != frame.getTransportId()) {
            clear();
            transport = frame.getTransportId();
        }
        if (revision == frame.getPresentation()
            .getScreenRevision()) return;
        revision = frame.getPresentation()
            .getScreenRevision();
        animation.prepare(
            frame.getPresentation()
                .getLayers(),
            frame.getPresentation()
                .getTransition(),
            System.nanoTime());
        pending = frame;
        prepareStarted = System.nanoTime();
        updateDemand();
    }

    public static void clear() {
        for (String ref : pinned) CanonicalMediaClient.unpin(ref);
        pinned.clear();
        demanded = Collections.emptySet();
        animation.clear();
        pending = null;
        transport = revision = -1;
        CanonicalMediaClient.sceneDemand(Collections.<String>emptySet());
    }

    private static void updateDemand() {
        Set<String> refs = animation.refs(System.nanoTime());
        if (pending != null)
            for (darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer l : pending.getPresentation()
                .getLayers()) refs.add(l.mediaRef);
        if (refs.equals(demanded)) return;
        for (String ref : new HashSet<String>(pinned)) if (!refs.contains(ref)) {
            CanonicalMediaClient.unpin(ref);
            pinned.remove(ref);
        }
        demanded = refs;
        CanonicalMediaClient.sceneDemand(refs);
    }

    public static int spriteCount() {
        return animation.sample(System.nanoTime())
            .size();
    }

    public static void draw(int width, int height) {
        long now = System.nanoTime();
        for (String ref : demanded) if (!pinned.contains(ref) && CanonicalMediaClient.pin(ref)) pinned.add(ref);
        if (pending != null) {
            boolean ready = true;
            for (darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer layer : pending.getPresentation()
                .getLayers()) ready &= CanonicalMediaTextures.get(layer.mediaRef) != null;
            if (ready || !pending.shouldPlayScreen() || now - prepareStarted >= 3000000000L) {
                animation.retarget(
                    revision,
                    pending.getPresentation()
                        .getLayers(),
                    pending.getPresentation()
                        .getTransition(),
                    now,
                    pending.shouldPlayScreen() && ready);
                if (animation.converged())
                    darkgrey.rpg.DarkGreyRpg.LOG.debug("Screen transition converged: bounded source sprite limit");
                pending = null;
            }
        }
        updateDemand();
        GL11.glPushAttrib(
            GL11.GL_ENABLE_BIT | GL11.GL_COLOR_BUFFER_BIT
                | GL11.GL_CURRENT_BIT
                | GL11.GL_TEXTURE_BIT
                | GL11.GL_SCISSOR_BIT
                | GL11.GL_DEPTH_BUFFER_BIT);
        try {
            GL11.glEnable(GL11.GL_TEXTURE_2D);
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            Minecraft mc = Minecraft.getMinecraft();
            for (ScreenTransition.Sprite s : animation.sample(now)) {
                if (s.alpha <= 0 || s.cw <= 0 || s.ch <= 0) continue;
                ResourceLocation texture = CanonicalMediaTextures.get(s.ref);
                GL11.glEnable(GL11.GL_SCISSOR_TEST);
                GL11.glScissor(
                    (int) Math.floor(s.cx * mc.displayWidth),
                    (int) Math.floor((1 - s.cy - s.ch) * mc.displayHeight),
                    (int) Math.ceil(s.cw * mc.displayWidth),
                    (int) Math.ceil(s.ch * mc.displayHeight));
                double x = s.x * width, y = s.y * height, w = s.w * width, h = s.h * height;
                if (texture == null) {
                    GL11.glDisable(GL11.GL_TEXTURE_2D);
                    GL11.glColor4d(.2, .2, .2, .35 * s.alpha);
                } else {
                    GL11.glEnable(GL11.GL_TEXTURE_2D);
                    mc.getTextureManager()
                        .bindTexture(texture);
                    GL11.glColor4d(1, 1, 1, s.alpha);
                }
                Tessellator t = Tessellator.instance;
                t.startDrawingQuads();
                t.addVertexWithUV(x, y + h, 0, 0, 1);
                t.addVertexWithUV(x + w, y + h, 0, 1, 1);
                t.addVertexWithUV(x + w, y, 0, 1, 0);
                t.addVertexWithUV(x, y, 0, 0, 0);
                t.draw();
            }
        } finally {
            GL11.glPopAttrib();
        }
    }
}
