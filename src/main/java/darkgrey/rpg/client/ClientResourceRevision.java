package darkgrey.rpg.client;

import net.minecraft.client.Minecraft;
import net.minecraft.client.resources.IReloadableResourceManager;
import net.minecraft.client.resources.IResourceManager;
import net.minecraft.client.resources.IResourceManagerReloadListener;

/** Invalidates local measured text when fonts, language or resource packs are reloaded. */
public final class ClientResourceRevision implements IResourceManagerReloadListener {

    private static long revision;

    public static long current() {
        return revision;
    }

    public static void register() {
        IResourceManager manager = Minecraft.getMinecraft()
            .getResourceManager();
        if (manager instanceof IReloadableResourceManager)
            ((IReloadableResourceManager) manager).registerReloadListener(new ClientResourceRevision());
    }

    @Override
    public void onResourceManagerReload(IResourceManager manager) {
        revision++;
        if (Boolean.getBoolean("darkgrey.dialogue.diagnostics")) {
            net.minecraft.client.gui.FontRenderer font = Minecraft.getMinecraft().fontRenderer;
            if (font == null) return;
            darkgrey.rpg.client.session.CanonicalDialogueLayout layout = new darkgrey.rpg.client.session.CanonicalDialogueLayout(
                320,
                240);
            int quote = (int) Math.ceil(Math.max(font.getStringWidth("「"), font.getStringWidth("」")) * 1.5);
            int wrap = (int) ((layout.textWidth - 2 * quote) / 1.5);
            int line = (int) Math.ceil(font.FONT_HEIGHT * 1.5);
            int rows = (layout.bodyBottom() - layout.bodyTop()) / line;
            darkgrey.rpg.DarkGreyRpg.LOG.info(
                "DIALOGUE_STANDARD_PROFILE unicode={} textLeft={} textRight={} bodyTop={} bodyBottom={} quote={} wrap={} rows={} raw={} safe={} W={} i={} CJK={}",
                font.getUnicodeFlag(),
                layout.textLeft,
                layout.right - 8,
                layout.bodyTop(),
                layout.bodyBottom(),
                quote,
                wrap,
                rows,
                wrap * rows,
                (int) Math.floor(wrap * rows * .9),
                font.getStringWidth("W"),
                font.getStringWidth("i"),
                font.getStringWidth("中"));
        }
    }
}
