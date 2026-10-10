package darkgrey.rpg.client.gui;

import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.util.ResourceLocation;

import org.lwjgl.opengl.GL11;

import cpw.mods.fml.relauncher.ReflectionHelper;
import darkgrey.rpg.client.session.DialogueFontScale;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Scoped font sampling: texture-object parameters must be restored explicitly, not just GL attributes. */
public final class DialogueFontDrawing {

    private static final Field FONT_TEXTURE = ReflectionHelper
        .findField(FontRenderer.class, "locationFontTexture", "field_111273_g");
    private static final ResourceLocation[] PAGES = new ResourceLocation[256];
    private static final Set<String> REPORTED = new LinkedHashSet<String>();
    private static boolean samplingProbeDone;

    private DialogueFontDrawing() {}

    public static int guiFactor() {
        Minecraft mc = Minecraft.getMinecraft();
        return new ScaledResolution(mc, mc.displayWidth, mc.displayHeight).getScaleFactor();
    }

    public static double scale() {
        return DialogueFontScale.effective(PlayerUiPreferences.textScale(), guiFactor());
    }

    public static String scaleLabel() {
        int requested = (int) Math.round(PlayerUiPreferences.textScale() * 100);
        int actual = (int) Math.round(scale() * 100);
        return requested == actual ? "" : requested + "% · 当前像素对齐为 " + actual + "%";
    }

    public static void draw(FontRenderer font, String text, double x, double y, double scale, int color) {
        if (text == null || text.isEmpty()) return;
        boolean diagnostic = Boolean.getBoolean("darkgrey.dialogue.font.diagnostics");
        int previousBinding = diagnostic ? GL11.glGetInteger(GL11.GL_TEXTURE_BINDING_2D) : 0;
        boolean previousBlend = diagnostic && GL11.glIsEnabled(GL11.GL_BLEND);
        boolean sampledNearest = true;
        int previousProgram = -1;
        boolean coverage = false;
        GL11.glPushAttrib(GL11.GL_TEXTURE_BIT | GL11.GL_ENABLE_BIT | GL11.GL_COLOR_BUFFER_BIT | GL11.GL_CURRENT_BIT);
        GL11.glPushMatrix();
        List<Filter> filters = new ArrayList<Filter>();
        Set<Integer> textureIds = new LinkedHashSet<Integer>();
        try {
            for (ResourceLocation texture : textures(font, text)) {
                Minecraft.getMinecraft()
                    .getTextureManager()
                    .bindTexture(texture);
                if (!textureIds.add(GL11.glGetInteger(GL11.GL_TEXTURE_BINDING_2D))) continue;
                Filter filter = new Filter();
                filters.add(filter);
                if (Boolean.getBoolean("darkgrey.dialogue.font.samplingProbe") && !samplingProbeDone) {
                    samplingProbeDone = true;
                    // Exercise a non-default incoming filter in the real GL context, then undo it.
                    nearest(GL11.GL_LINEAR, GL11.GL_LINEAR);
                    Filter linear = new Filter();
                    nearest(GL11.GL_NEAREST, GL11.GL_NEAREST);
                    linear.restore();
                    boolean restored = GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MIN_FILTER)
                        == GL11.GL_LINEAR
                        && GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MAG_FILTER) == GL11.GL_LINEAR;
                    filter.restore();
                    darkgrey.rpg.DarkGreyRpg.LOG.info("DIALOGUE_FONT_SAMPLING_PROBE linearRestore={}", restored);
                    if (!restored) throw new IllegalStateException("Font sampling state was not restored");
                }
                nearest(GL11.GL_NEAREST, GL11.GL_NEAREST);
                if (diagnostic)
                    sampledNearest &= GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MIN_FILTER)
                        == GL11.GL_NEAREST
                        && GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MAG_FILTER) == GL11.GL_NEAREST;
            }
            int factor = guiFactor();
            GL11.glTranslated(DialogueFontScale.snap(x, factor), DialogueFontScale.snap(y, factor), 0);
            GL11.glScaled(scale, scale, 1);
            previousProgram = FontCoverageDrawing.begin(scale, factor, text);
            coverage = previousProgram >= 0;
            font.drawString(text, 0, 0, color);
        } finally {
            FontCoverageDrawing.end(previousProgram);
            boolean restored = true;
            for (Filter filter : filters) {
                filter.restore();
                if (diagnostic) restored &= filter.matches();
            }
            GL11.glPopMatrix();
            GL11.glPopAttrib();
            if (diagnostic) {
                restored &= GL11.glGetInteger(GL11.GL_TEXTURE_BINDING_2D) == previousBinding
                    && GL11.glIsEnabled(GL11.GL_BLEND) == previousBlend;
                if (coverage)
                    restored &= GL11.glGetInteger(org.lwjgl.opengl.GL20.GL_CURRENT_PROGRAM) == previousProgram;
                String key = guiFactor() + ":"
                    + PlayerUiPreferences.textScale()
                    + ":"
                    + font.getUnicodeFlag()
                    + ":"
                    + scale;
                if (!restored || !sampledNearest) throw new IllegalStateException("Font texture sampling leaked");
                if (REPORTED.size() < 64 && REPORTED.add(key)) darkgrey.rpg.DarkGreyRpg.LOG.info(
                    "DIALOGUE_FONT factor={} requested={} effective={} nearest={} filtersBindingBlendRestored={} coverage={}",
                    guiFactor(),
                    PlayerUiPreferences.textScale(),
                    scale,
                    sampledNearest,
                    restored,
                    coverage);
            }
        }
    }

    private static Set<ResourceLocation> textures(FontRenderer font, String text) {
        Set<ResourceLocation> textures = new LinkedHashSet<ResourceLocation>();
        try {
            textures.add((ResourceLocation) FONT_TEXTURE.get(font));
        } catch (IllegalAccessException error) {
            throw new IllegalStateException("Cannot read active font texture", error);
        }
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            if (c == '\u00a7' && i + 1 < text.length()) {
                i++;
                continue;
            }
            // Include page zero as well: extended Latin may be drawn via Unicode with unicodeFlag off.
            int page = c / 256;
            if (PAGES[page] == null)
                PAGES[page] = new ResourceLocation(String.format("textures/font/unicode_page_%02x.png", page));
            textures.add(PAGES[page]);
        }
        return textures;
    }

    private static void nearest(int min, int mag) {
        GL11.glTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MIN_FILTER, min);
        GL11.glTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MAG_FILTER, mag);
    }

    private static final class Filter {

        final int texture = GL11.glGetInteger(GL11.GL_TEXTURE_BINDING_2D);
        final int min = GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MIN_FILTER);
        final int mag = GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MAG_FILTER);

        void restore() {
            GL11.glBindTexture(GL11.GL_TEXTURE_2D, texture);
            nearest(min, mag);
        }

        boolean matches() {
            return GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MIN_FILTER) == min
                && GL11.glGetTexParameteri(GL11.GL_TEXTURE_2D, GL11.GL_TEXTURE_MAG_FILTER) == mag;
        }
    }
}
