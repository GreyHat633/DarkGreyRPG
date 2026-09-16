package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.resources.I18n;

import darkgrey.rpg.client.session.CanonicalDialogueLayout;
import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Stateless drawing shared by foreground and render-only underlay. */
public final class CanonicalDialogueRenderer {

    private static long portraitWaitStarted;
    private static long portraitTransport = -1;
    private static String waitingPortrait;

    private CanonicalDialogueRenderer() {}

    private static void drawPortrait(net.minecraft.util.ResourceLocation texture, int x, int y, int size,
        double aspect) {
        int width = aspect >= 1 ? size : Math.max(1, (int) (size * aspect));
        int height = aspect <= 1 ? size : Math.max(1, (int) (size / aspect));
        x += (size - width) / 2;
        y += (size - height) / 2;
        net.minecraft.client.Minecraft.getMinecraft()
            .getTextureManager()
            .bindTexture(texture);
        org.lwjgl.opengl.GL11.glColor4f(1, 1, 1, 1);
        boolean blending = org.lwjgl.opengl.GL11.glIsEnabled(org.lwjgl.opengl.GL11.GL_BLEND);
        org.lwjgl.opengl.GL11.glEnable(org.lwjgl.opengl.GL11.GL_BLEND);
        org.lwjgl.opengl.GL11
            .glBlendFunc(org.lwjgl.opengl.GL11.GL_SRC_ALPHA, org.lwjgl.opengl.GL11.GL_ONE_MINUS_SRC_ALPHA);
        net.minecraft.client.renderer.Tessellator tessellator = net.minecraft.client.renderer.Tessellator.instance;
        tessellator.startDrawingQuads();
        tessellator.addVertexWithUV(x, y + height, 0, 0, 1);
        tessellator.addVertexWithUV(x + width, y + height, 0, 1, 1);
        tessellator.addVertexWithUV(x + width, y, 0, 1, 0);
        tessellator.addVertexWithUV(x, y, 0, 0, 0);
        tessellator.draw();
        if (!blending) org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_BLEND);
    }

    public static int draw(FontRenderer font, int width, int height, CanonicalSessionFrame frame, int scrollLine,
        boolean awaitingServer) {
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
        int left = layout.left;
        int top = layout.top;
        int right = layout.right;
        int bottom = layout.bottom;
        String speaker = CanonicalSessionClientController.getVisibleSpeaker();
        String text = CanonicalSessionClientController.getVisibleText();
        String portraitRef = speaker.isEmpty() ? null : CanonicalSessionClientController.getVisiblePortraitRef();
        net.minecraft.util.ResourceLocation portrait = portraitRef == null ? null
            : darkgrey.rpg.media.CanonicalMediaTextures.get(portraitRef);
        if (portraitTransport != frame.getTransportId() || !java.util.Objects.equals(waitingPortrait, portraitRef)) {
            portraitTransport = frame.getTransportId();
            waitingPortrait = portraitRef;
            portraitWaitStarted = System.nanoTime();
        }
        // Warmed portraits are ready on the first frame. Allow a short decode grace period,
        // but a missing/corrupt/remote image must never hide the dialogue indefinitely.
        if (portraitRef != null && portrait == null && System.nanoTime() - portraitWaitStarted < 150000000L)
            return scrollLine;
        Gui.drawRect(left, top, right, bottom, 0xCC161616);
        Gui.drawRect(left, top, right, top + 1, 0xFFC0C0C0);
        Gui.drawRect(left, bottom - 1, right, bottom, 0xFF888888);
        Gui.drawRect(left, top, left + 1, bottom, 0xFF888888);
        Gui.drawRect(right - 1, top, right, bottom, 0xFF888888);
        int textLeft = left + 8;
        if (portraitRef != null) {
            if (portrait != null) drawPortrait(
                portrait,
                left + 8,
                top + 8,
                layout.portraitSize,
                darkgrey.rpg.media.CanonicalMediaTextures.aspect(portraitRef));
            else Gui
                .drawRect(left + 8, top + 8, left + 8 + layout.portraitSize, top + 8 + layout.portraitSize, 0x55333333);
            textLeft = layout.textLeft;
        }
        int textWidth = right - textLeft - 8;
        int textTop = top + 7;
        if (!speaker.isEmpty()) {
            font.drawString(font.trimStringToWidth(speaker, textWidth), textLeft, textTop, DgrUiPalette.TEXT);
            textTop += font.FONT_HEIGHT + 5;
            Gui.drawRect(textLeft, textTop, right - 8, textTop + 1, 0xFF888888);
            textTop += 5;
            text = "「" + text + "」";
        }
        int textBottom = bottom - 15;
        List<String> lines = font.listFormattedStringToWidth(text, textWidth);
        int visibleLines = Math.max(1, (textBottom - textTop) / font.FONT_HEIGHT);
        int maximumScroll = Math.max(0, lines.size() - visibleLines);
        scrollLine = Math.min(scrollLine, maximumScroll);
        for (int index = 0; index < visibleLines && index + scrollLine < lines.size(); index++) {
            font.drawString(lines.get(index + scrollLine), textLeft, textTop + index * font.FONT_HEIGHT, 0xFFEEEEEE);
        }
        String hint = awaitingServer ? ""
            : maximumScroll > 0 ? I18n.format("gui.darkgrey_rpg.dialogue.scroll")
                : frame.canContinue() ? I18n.format("gui.darkgrey_rpg.dialogue.continue") : "";
        font.drawString(font.trimStringToWidth(hint, textWidth), textLeft, bottom - 11, 0xFFAAAAAA);
        return scrollLine;
    }
}
