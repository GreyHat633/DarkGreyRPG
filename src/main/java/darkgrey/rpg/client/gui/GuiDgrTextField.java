package darkgrey.rpg.client.gui;

import java.lang.reflect.Field;

import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.GuiTextField;

import org.lwjgl.opengl.GL11;

import cpw.mods.fml.relauncher.ReflectionHelper;

/** Retains vanilla editing, scrolling and selection; only the text paint omits the shadow pass. */
public final class GuiDgrTextField extends GuiTextField {

    private static final Field SCROLL = field("lineScrollOffset", "field_146225_q");
    private static final Field COUNTER = field("cursorCounter", "field_146214_l");
    private static final Field ENABLED = field("isEnabled", "field_146226_p");
    private static final Field COLOR = field("enabledColor", "field_146222_t");
    private static final Field DISABLED_COLOR = field("disabledColor", "field_146221_u");
    private final FontRenderer font;

    public GuiDgrTextField(FontRenderer font, int x, int y, int width, int height) {
        super(font, x, y, width, height);
        this.font = font;
    }

    private static Field field(String mcp, String srg) {
        return ReflectionHelper.findField(GuiTextField.class, mcp, srg);
    }

    private int integer(Field field) {
        try {
            return field.getInt(this);
        } catch (IllegalAccessException error) {
            throw new IllegalStateException("Cannot read text field paint state", error);
        }
    }

    @Override
    public void drawTextBox() {
        if (!getVisible()) return;
        if (getEnableBackgroundDrawing()) {
            drawRect(xPosition - 1, yPosition - 1, xPosition + width + 1, yPosition + height + 1, DgrUiPalette.BORDER);
            drawRect(xPosition, yPosition, xPosition + width, yPosition + height, DgrUiPalette.SUB_PANEL);
        }
        boolean enabled;
        try {
            enabled = ENABLED.getBoolean(this);
        } catch (IllegalAccessException error) {
            throw new IllegalStateException("Cannot read text field enabled state", error);
        }
        int color = integer(enabled ? COLOR : DISABLED_COLOR);
        int offset = integer(SCROLL);
        String text = getText();
        String visible = font.trimStringToWidth(text.substring(offset), getWidth());
        int cursor = getCursorPosition() - offset, selection = getSelectionEnd() - offset;
        boolean cursorVisible = cursor >= 0 && cursor <= visible.length();
        int left = getEnableBackgroundDrawing() ? xPosition + 4 : xPosition;
        int top = getEnableBackgroundDrawing() ? yPosition + (height - 8) / 2 : yPosition;
        String before = cursorVisible ? visible.substring(0, cursor) : visible;
        DgrUiText.left(font, before, left, top, color);
        int end = left + font.getStringWidth(before);
        if (cursorVisible && cursor < visible.length())
            DgrUiText.left(font, visible.substring(cursor), end, top, color);
        boolean bar = getCursorPosition() < text.length() || text.length() >= getMaxStringLength();
        int cursorX = cursorVisible ? end : cursor > 0 ? left + width : left;
        if (isFocused() && integer(COUNTER) / 6 % 2 == 0 && cursorVisible) {
            if (bar) drawRect(cursorX - 1, top - 1, cursorX, top + 1 + font.FONT_HEIGHT, DgrUiPalette.TEXT);
            else DgrUiText.left(font, "_", cursorX, top, color);
        }
        selection = Math.max(0, Math.min(selection, visible.length()));
        if (selection != cursor) {
            int selectionX = left + font.getStringWidth(visible.substring(0, selection));
            int a = Math.min(cursorX, selectionX), b = Math.min(xPosition + width, Math.max(cursorX, selectionX));
            GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
            try {
                GL11.glEnable(GL11.GL_COLOR_LOGIC_OP);
                GL11.glLogicOp(GL11.GL_OR_REVERSE);
                drawRect(a, top - 1, b, top + 1 + font.FONT_HEIGHT, 0xFF0000FF);
            } finally {
                GL11.glPopAttrib();
            }
        }
    }
}
