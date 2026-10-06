package darkgrey.rpg.client.gui;

import java.nio.IntBuffer;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.ScaledResolution;

import org.lwjgl.BufferUtils;
import org.lwjgl.opengl.GL11;

/** Scoped clipping for partial rows during pixel scrolling. */
public final class GuiScrollClip implements AutoCloseable {

    public GuiScrollClip(int left, int top, int right, int bottom) {
        Minecraft mc = Minecraft.getMinecraft();
        int factor = new ScaledResolution(mc, mc.displayWidth, mc.displayHeight).getScaleFactor();
        int x = left * factor, y = mc.displayHeight - bottom * factor;
        int w = Math.max(0, right - left) * factor, h = Math.max(0, bottom - top) * factor;
        if (GL11.glIsEnabled(GL11.GL_SCISSOR_TEST)) {
            IntBuffer existing = BufferUtils.createIntBuffer(16);
            GL11.glGetInteger(GL11.GL_SCISSOR_BOX, existing);
            int rightEdge = Math.min(x + w, existing.get(0) + existing.get(2));
            int topEdge = Math.min(y + h, existing.get(1) + existing.get(3));
            x = Math.max(x, existing.get(0));
            y = Math.max(y, existing.get(1));
            w = Math.max(0, rightEdge - x);
            h = Math.max(0, topEdge - y);
        }
        GL11.glPushAttrib(GL11.GL_SCISSOR_BIT);
        GL11.glEnable(GL11.GL_SCISSOR_TEST);
        GL11.glScissor(x, y, w, h);
    }

    @Override
    public void close() {
        GL11.glPopAttrib();
    }
}
