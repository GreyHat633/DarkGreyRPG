package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.Tessellator;
import net.minecraft.util.ResourceLocation;

import org.lwjgl.opengl.GL11;

import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation.Layer;

/** Full replacement scene, rendered behind dialogue at the current scaled resolution. */
public final class CanonicalSessionScene {

    private static List<Layer> layers = Collections.emptyList();

    private CanonicalSessionScene() {}

    public static void present(CanonicalSessionFrame frame) {
        layers = new ArrayList<Layer>(
            frame.getPresentation()
                .getLayers());
        Collections.sort(layers, new Comparator<Layer>() {

            @Override
            public int compare(Layer a, Layer b) {
                return Integer.compare(a.z, b.z);
            }
        });
    }

    public static void clear() {
        layers = Collections.emptyList();
    }

    public static void draw(int width, int height) {
        if (layers.isEmpty()) return;
        GL11.glPushAttrib(GL11.GL_ENABLE_BIT | GL11.GL_COLOR_BUFFER_BIT | GL11.GL_CURRENT_BIT | GL11.GL_TEXTURE_BIT);
        try {
            GL11.glEnable(GL11.GL_TEXTURE_2D);
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            GL11.glColor4f(1, 1, 1, 1);
            for (Layer layer : layers) {
                ResourceLocation texture = CanonicalMediaTextures.get(layer.mediaRef);
                if (texture == null) continue;
                Minecraft.getMinecraft()
                    .getTextureManager()
                    .bindTexture(texture);
                double w = layer.width * width, h = layer.height * height;
                double x = layer.x * width - layer.anchorX * w, y = layer.y * height - layer.anchorY * h;
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
