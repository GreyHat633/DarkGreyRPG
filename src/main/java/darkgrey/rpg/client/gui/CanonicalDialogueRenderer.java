package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;

import darkgrey.rpg.client.session.CanonicalDialogueLayout;
import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Stateless drawing shared by foreground and render-only underlay. */
public final class CanonicalDialogueRenderer {

    private static String displayedPortrait;
    private static long portraitTransport = -1;

    private CanonicalDialogueRenderer() {}

    public static void clear() {
        displayedPortrait = null;
        portraitTransport = -1;
    }

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

    public static void draw(final FontRenderer font, int width, int height, CanonicalSessionFrame frame,
        boolean awaitingServer) {
        double scale = DialogueFontDrawing.scale();
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height, scale, font.FONT_HEIGHT);
        int left = layout.left;
        int top = layout.top;
        int right = layout.right;
        int bottom = layout.bottom;
        darkgrey.rpg.media.MediaLatencyTrace.draw();
        String speaker = CanonicalSessionClientController.getVisibleSpeaker();
        String portraitRef = speaker.isEmpty() ? null : CanonicalSessionClientController.getVisiblePortraitRef();
        net.minecraft.util.ResourceLocation portrait = portraitRef == null ? null
            : darkgrey.rpg.media.CanonicalMediaTextures.get(portraitRef);
        if (portraitTransport != frame.getTransportId()) {
            portraitTransport = frame.getTransportId();
            displayedPortrait = null;
        }
        if (portraitRef == null) displayedPortrait = null;
        else if (portrait != null) displayedPortrait = portraitRef;
        else if (!portraitRef.equals(displayedPortrait)) displayedPortrait = null;
        darkgrey.rpg.client.session.DialogueHistoryClient.presented(frame, speaker);
        Gui.drawRect(left, top, right, bottom, DgrUiPalette.dialoguePanel());
        Gui.drawRect(left, top, right, top + 1, DgrUiPalette.SELECTED_BORDER);
        Gui.drawRect(left, bottom - 1, right, bottom, DgrUiPalette.BORDER);
        Gui.drawRect(left, top, left + 1, bottom, DgrUiPalette.BORDER);
        Gui.drawRect(right - 1, top, right, bottom, DgrUiPalette.BORDER);
        int textLeft = left + 8;
        if (portraitRef != null) {
            if (portrait != null) drawPortrait(
                portrait,
                layout.portraitLeft(),
                layout.portraitTop(),
                layout.portraitSize,
                darkgrey.rpg.media.CanonicalMediaTextures.aspect(displayedPortrait));
            else Gui.drawRect(
                layout.portraitLeft(),
                layout.portraitTop(),
                layout.portraitLeft() + layout.portraitSize,
                layout.portraitTop() + layout.portraitSize,
                0x55333333);
            int px = layout.portraitLeft(), py = layout.portraitTop(), size = layout.portraitSize;
            Gui.drawRect(px - 1, py - 1, px + size + 1, py, DgrUiPalette.BORDER);
            Gui.drawRect(px - 1, py + size, px + size + 1, py + size + 1, DgrUiPalette.BORDER);
            Gui.drawRect(px - 1, py, px, py + size, DgrUiPalette.BORDER);
            Gui.drawRect(px + size, py, px + size + 1, py + size, DgrUiPalette.BORDER);
            textLeft = layout.textLeft;
        }
        int textWidth = right - textLeft - 8;
        // Decoration has its own narrow gutters and never changes the source text.
        int quote = speaker.isEmpty() ? 0
            : (int) Math.ceil(Math.max(font.getStringWidth("「"), font.getStringWidth("」")) * scale);
        int wrapWidth = Math.max(1, (int) ((textWidth - 2 * quote) / scale));
        int lineHeight = (int) Math.ceil(font.FONT_HEIGHT * scale);
        int textTop = layout.bodyTop();
        if (!speaker.isEmpty()) {
            int nameWidth = layout.speakerWidth(portraitRef != null);
            String name = font.trimStringToWidth(speaker, Math.max(1, (int) (nameWidth / scale)));
            double nameLeft = layout.speakerLeft(portraitRef != null);
            if (portraitRef != null) nameLeft += (nameWidth - font.getStringWidth(name) * scale) / 2;
            DialogueFontDrawing
                .draw(font, name, nameLeft, layout.speakerTop(lineHeight), scale, DgrUiPalette.SELECTED_BORDER);
        }
        Gui.drawRect(left + 8, layout.dividerTop(), right - 8, layout.dividerTop() + 1, DgrUiPalette.BORDER);
        int visibleLines = Math.max(1, (layout.bodyBottom() - textTop) / lineHeight);
        darkgrey.rpg.client.session.CanonicalSessionClientModel model = CanonicalSessionClientController
            .presentationModel();
        model.layout(
            width + ":"
                + height
                + ":"
                + scale
                + ":"
                + portraitRef
                + ":"
                + speaker.isEmpty()
                + ":"
                + font.getUnicodeFlag()
                + ":"
                + darkgrey.rpg.client.ClientResourceRevision.current(),
            wrapWidth,
            visibleLines,
            new darkgrey.rpg.client.session.DialogueDisplayPages.Metrics() {

                @Override
                public double advance(String cluster, boolean bold) {
                    return font.getStringWidth((bold ? "\u00a7l" : "") + cluster);
                }
            });
        List<String> lines = model.getDisplayLines();
        if (quote > 0) drawText(font, "「", textLeft, textTop, scale, DgrUiPalette.TEXT);
        for (int index = 0; index < lines.size(); index++) {
            drawText(font, lines.get(index), textLeft + quote, textTop + index * lineHeight, scale, DgrUiPalette.TEXT);
        }
        if (quote > 0 && !lines.isEmpty() && model.displayTextComplete()) drawText(
            font,
            "」",
            textLeft + quote + (int) Math.ceil(font.getStringWidth(lines.get(lines.size() - 1)) * scale),
            textTop + (lines.size() - 1) * lineHeight,
            scale,
            DgrUiPalette.TEXT);
        if (!awaitingServer && !model.awaitingServer() && frame.canContinue()) {
            int hintX = right - 17;
            int hintY = bottom - 12;
            org.lwjgl.opengl.GL11.glPushMatrix();
            try {
                org.lwjgl.opengl.GL11.glTranslated(0, Math.round(1.5 * Math.sin(System.nanoTime() / 400000000.0)), 0);
                if (model.automatic()) DgrUiText.left(font, "…", hintX, hintY, DgrUiPalette.SECONDARY);
                else {
                    // Outline triangle drawn geometrically, independent of font glyph coverage.
                    for (int row = 0; row < 5; row++) {
                        Gui.drawRect(
                            hintX + row,
                            hintY + row,
                            hintX + row + 1,
                            hintY + row + 1,
                            DgrUiPalette.SECONDARY);
                        Gui.drawRect(
                            hintX + 8 - row,
                            hintY + row,
                            hintX + 9 - row,
                            hintY + row + 1,
                            DgrUiPalette.SECONDARY);
                    }
                    Gui.drawRect(hintX, hintY, hintX + 9, hintY + 1, DgrUiPalette.SECONDARY);
                }
            } finally {
                org.lwjgl.opengl.GL11.glPopMatrix();
            }
        }
    }

    public static void drawText(FontRenderer font, String text, int x, int y, double scale, int color) {
        DialogueFontDrawing.draw(font, text, x, y, scale, color);
    }
}
