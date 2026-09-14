package darkgrey.rpg.title;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.ScaledResolution;

import org.lwjgl.opengl.GL11;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalTitleComplete;
import darkgrey.rpg.network.message.canonical.CanonicalTitleFrame;
import darkgrey.rpg.story.canonical.runtime.CanonicalTitleConfiguration;

/** Independent Story HUD. Session UI and media ownership are never consulted. */
public final class CanonicalTitleClient {

    private static net.minecraft.world.World world;
    private static long token;
    private static long started;
    private static CanonicalTitleConfiguration title;

    private CanonicalTitleClient() {}

    private static void world() {
        if (world != Minecraft.getMinecraft().theWorld) {
            world = Minecraft.getMinecraft().theWorld;
            token = 0;
            title = null;
        }
    }

    public static void accept(CanonicalTitleFrame frame) {
        world();
        if (world == null || frame.getToken() < token || (frame.getToken() == token && frame.getTitle() != null))
            return;
        token = frame.getToken();
        title = frame.getTitle();
        started = System.nanoTime();
    }

    public static void tick() {
        world();
        Minecraft mc = Minecraft.getMinecraft();
        if (world == null || mc.thePlayer == null || mc.thePlayer.isDead || mc.thePlayer.getHealth() <= 0) {
            title = null;
            return;
        }
        if (title != null && elapsed() >= title.duration()) {
            title = null;
            DialogueNetwork.CHANNEL.sendToServer(new CanonicalTitleComplete(token));
        }
    }

    private static double elapsed() {
        return (System.nanoTime() - started) / 1000000000.0;
    }

    public static void draw() {
        if (title == null || world != Minecraft.getMinecraft().theWorld) return;
        Minecraft mc = Minecraft.getMinecraft();
        ScaledResolution scaled = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight);
        int alpha = Math.round(title.alpha(elapsed()) * 255);
        if (alpha <= 3) return;
        GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
        GL11.glPushMatrix();
        try {
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            line(title.main, scaled.getScaledWidth(), scaled.getScaledHeight() * 0.40f, 3, alpha);
            if (!title.subtitle.isEmpty())
                line(title.subtitle, scaled.getScaledWidth(), scaled.getScaledHeight() * 0.40f + 35, 1.5f, alpha);
        } finally {
            GL11.glPopMatrix();
            GL11.glPopAttrib();
        }
    }

    private static void line(String text, int width, float y, float desiredScale, int alpha) {
        Minecraft mc = Minecraft.getMinecraft();
        float scale = Math.min(desiredScale, width * 0.9f / Math.max(1, mc.fontRenderer.getStringWidth(text)));
        GL11.glPushMatrix();
        GL11.glTranslatef(width / 2f, y, 0);
        GL11.glScalef(scale, scale, 1);
        mc.fontRenderer
            .drawStringWithShadow(text, -mc.fontRenderer.getStringWidth(text) / 2, 0, (alpha << 24) | 0xFFFFFF);
        GL11.glPopMatrix();
    }
}
