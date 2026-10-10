package darkgrey.rpg.client.gui;

/** Shared semantic colors, updated on the client thread when local preferences change. */
public final class DgrUiPalette {

    private DgrUiPalette() {}

    public static int WINDOW_PANEL = 0xFF161616;
    public static int PANEL = WINDOW_PANEL;
    public static int WINDOW_CONTENT = 0xCC202020;
    public static int SUB_PANEL = 0xFF202020;
    public static int BORDER = 0xFF888888;
    public static int SELECTED_BORDER = 0xFFDDDDDD;
    public static int SELECTED_FILL = 0xFF101010;
    public static int HOVER = 0xFF505050;
    public static int STORY_TEXT = 0xFF8ECBE8;
    public static int TEXT = 0xFFE8E8E8;
    public static int SECONDARY = 0xFFAAAAAA;
    public static int DISABLED = 0xFF777777;
    public static int SLOT_BORDER = 0xFFCCCCCC;
    public static final int MODAL_MASK = 0xB0000000;
    public static final int WORLD_LABEL_TEXT = 0xFFF0F0F0;
    public static int HUD_PANEL = 0x99161616;

    public static void apply() {
        darkgrey.rpg.client.session.PlayerUiPreferences.Theme theme = darkgrey.rpg.client.session.PlayerUiPreferences
            .theme();
        boolean dark = theme == darkgrey.rpg.client.session.PlayerUiPreferences.Theme.CHARCOAL;
        boolean azure = theme == darkgrey.rpg.client.session.PlayerUiPreferences.Theme.AZURE;
        // Foreground windows must cover the text of lower HUDs and render-only dialogue underlays.
        WINDOW_PANEL = dark ? 0xFF161616 : azure ? 0xFFEAF7FC : 0xFFF5F5F5;
        PANEL = WINDOW_PANEL;
        HUD_PANEL = 0x99000000 | (WINDOW_PANEL & 0xFFFFFF);
        WINDOW_CONTENT = dark ? 0xCC202020 : 0xEEFDFDFD;
        SUB_PANEL = dark ? 0xFF202020 : azure ? 0xFFE0F2FA : 0xFFEAEAEA;
        BORDER = dark ? 0xFF888888 : 0xFF87959A;
        SELECTED_BORDER = dark ? 0xFFDDDDDD : azure ? 0xFF087EB3 : 0xFF414141;
        SELECTED_FILL = dark ? 0xFF101010 : azure ? 0xFF8BC5E3 : 0xFFBDBDBD;
        HOVER = dark ? 0xFF505050 : azure ? 0xFFBDE7FA : 0xFFD6D6D6;
        STORY_TEXT = dark ? 0xFF8ECBE8 : azure ? 0xFF075A85 : 0xFF305E75;
        TEXT = dark ? 0xFFE8E8E8 : 0xFF202A30;
        SECONDARY = dark ? 0xFFAAAAAA : 0xFF52626B;
        DISABLED = dark ? 0xFF777777 : 0xFF7A8489;
        SLOT_BORDER = dark ? 0xFFCCCCCC : 0xFF677B85;
    }

    public static int dialoguePanel() {
        return ((int) Math.round(darkgrey.rpg.client.session.PlayerUiPreferences.opacity() * 255) << 24)
            | (WINDOW_PANEL & 0xFFFFFF);
    }
}
