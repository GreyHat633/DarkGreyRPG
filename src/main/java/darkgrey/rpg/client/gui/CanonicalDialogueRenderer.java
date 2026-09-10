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

    private CanonicalDialogueRenderer() {}

    public static int draw(FontRenderer font, int width, int height, CanonicalSessionFrame frame, int scrollLine,
        boolean awaitingServer) {
        CanonicalDialogueLayout layout = new CanonicalDialogueLayout(width, height);
        int left = layout.left;
        int top = layout.top;
        int right = layout.right;
        int bottom = layout.bottom;
        Gui.drawRect(left, top, right, bottom, 0xCC161616);
        Gui.drawRect(left, top, right, top + 1, 0xFFC0C0C0);
        Gui.drawRect(left, bottom - 1, right, bottom, 0xFF888888);
        Gui.drawRect(left, top, left + 1, bottom, 0xFF888888);
        Gui.drawRect(right - 1, top, right, bottom, 0xFF888888);
        // Reserved empty portrait slot: no authored portrait data in this release.
        Gui.drawRect(left + 8, top + 8, left + 8 + layout.portraitSize, top + 8 + layout.portraitSize, 0xFF777777);
        Gui.drawRect(left + 9, top + 9, left + 7 + layout.portraitSize, top + 7 + layout.portraitSize, 0xFF202020);
        String speaker = CanonicalSessionClientController.getVisibleSpeaker();
        String text = CanonicalSessionClientController.getVisibleText();
        int textTop = top + 7;
        if (!speaker.isEmpty()) {
            font.drawString(
                font.trimStringToWidth(speaker, layout.textWidth),
                layout.textLeft,
                textTop,
                DgrUiPalette.TEXT);
            textTop += font.FONT_HEIGHT + 3;
        }
        int textBottom = bottom - 15;
        List<String> lines = font.listFormattedStringToWidth(text, layout.textWidth);
        int visibleLines = Math.max(1, (textBottom - textTop) / font.FONT_HEIGHT);
        int maximumScroll = Math.max(0, lines.size() - visibleLines);
        scrollLine = Math.min(scrollLine, maximumScroll);
        for (int index = 0; index < visibleLines && index + scrollLine < lines.size(); index++) {
            font.drawString(
                lines.get(index + scrollLine),
                layout.textLeft,
                textTop + index * font.FONT_HEIGHT,
                0xFFEEEEEE);
        }
        String hint = awaitingServer ? ""
            : maximumScroll > 0 ? I18n.format("gui.darkgrey_rpg.dialogue.scroll")
                : frame.canContinue() ? I18n.format("gui.darkgrey_rpg.dialogue.continue") : "";
        font.drawString(font.trimStringToWidth(hint, layout.textWidth), layout.textLeft, bottom - 11, 0xFFAAAAAA);
        return scrollLine;
    }
}
