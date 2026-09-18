package darkgrey.rpg.gramophone;

import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.GuiTextField;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.client.gui.DgrUiPalette;
import darkgrey.rpg.client.gui.UtilityWindowChrome;
import darkgrey.rpg.client.gui.UtilityWindowGeometry;

/** Editor draft, local preview, and explicit upload confirmation have separate ownership. */
public final class GuiGramophone extends GuiScreen {

    private GramophonePacket config;
    private GuiTextField source, radius;
    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(280, 248, 400, 248);
    private boolean initialized, enabled, redstone, showRange, local, confirming, uploading, seeking;
    private String status = "仅支持公开可完整播放的 QQ / 网易云单曲";
    private int left, top, panelWidth;
    private GramophoneDraft draft;

    public GuiGramophone(GramophonePacket config) {
        this.config = config;
        enabled = config.enabled;
        redstone = config.redstone;
        local = config.source.startsWith("local:");
        if (local) status = "本地 MP3：32 MiB / 15 分钟以内；试听不上传";
        if (!config.status.isEmpty()) status = config.status;
    }

    @Override
    public void initGui() {
        UtilityWindowChrome.open("gramophone", geometry, width, height, initialized);
        initialized = true;
        layout();
        if (draft == null) draft = new GramophoneDraft(this);
        Keyboard.enableRepeatEvents(true);
    }

    private void layout() {
        String oldSource = source == null ? (local ? "" : config.source) : source.getText();
        String oldRadius = radius == null ? Integer.toString(config.radius) : radius.getText();
        left = geometry.x;
        top = geometry.y;
        panelWidth = geometry.width;
        radius = new GuiTextField(fontRendererObj, left + 90, top + 59, 65, 20);
        radius.setMaxStringLength(3);
        radius.setText(oldRadius);
        source = new GuiTextField(fontRendererObj, left + 12, top + 115, panelWidth - 24, 18);
        source.setMaxStringLength(2048);
        source.setText(oldSource);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    private void button(int x, int y, int w, String text) {
        drawRect(x, y, x + w, y + 20, DgrUiPalette.SUB_PANEL);
        fontRendererObj.drawString(text, x + 5, y + 6, DgrUiPalette.TEXT);
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (!showRange) drawDefaultBackground();
        drawRect(
            left,
            top,
            left + panelWidth,
            top + geometry.height,
            showRange ? (DgrUiPalette.WINDOW_PANEL & 0x00FFFFFF) | 0x70000000 : DgrUiPalette.WINDOW_PANEL);
        drawCenteredString(fontRendererObj, "环境留声机", left + panelWidth / 2, top + 8, DgrUiPalette.TEXT);
        button(left + 12, top + 30, 110, enabled ? "播放：开启" : "播放：关闭");
        button(left + 132, top + 30, panelWidth - 144, redstone ? "红石控制：开启" : "红石控制：关闭");
        fontRendererObj.drawString("范围（0–128）", left + 12, top + 65, DgrUiPalette.TEXT);
        radius.drawTextBox();
        button(left + 170, top + 59, panelWidth - 182, showRange ? "隐藏范围" : "显示范围");
        button(left + 12, top + 87, 100, local ? "来源：本地" : "来源：在线");
        button(left + 122, top + 87, panelWidth - 134, local ? "导入 MP3" : "解析并准备试听");
        if (local) fontRendererObj.drawString(
            fontRendererObj.trimStringToWidth(
                draft.media == null && config.source.startsWith("local:") ? "已保存本地音乐；可导入替换" : draft.label,
                panelWidth - 24),
            left + 12,
            top + 119,
            DgrUiPalette.TEXT);
        else source.drawTextBox();
        button(left + 12, top + 142, 65, draft.playing() ? "暂停试听" : "播放试听");
        int waveLeft = left + 86, waveWidth = panelWidth - 98;
        drawRect(waveLeft, top + 139, waveLeft + waveWidth, top + 162, DgrUiPalette.SUB_PANEL);
        if (draft.media != null) {
            for (int i = 0; i < 256; i++) {
                int x = waveLeft + i * waveWidth / 256, h = Math.max(1, (int) (draft.media.envelope[i] * 10));
                drawRect(x, top + 150 - h, x + 1, top + 151 + h, DgrUiPalette.SECONDARY);
            }
            int position = waveLeft + (int) (draft.seconds() / draft.media.seconds * waveWidth);
            drawRect(position, top + 139, position + 1, top + 163, DgrUiPalette.TEXT);
            fontRendererObj.drawString(
                time(draft.seconds()) + " / " + time(draft.media.seconds) + "（拖动试听进度）",
                waveLeft,
                top + 165,
                DgrUiPalette.SECONDARY);
        }
        fontRendererObj.drawSplitString(status, left + 12, top + 180, panelWidth - 24, DgrUiPalette.TEXT);
        String playback = !local && draft.media == null && !draft.label.equals("尚未导入") ? draft.label
            : GramophoneClient.status(config.key());
        fontRendererObj.drawString(
            fontRendererObj.trimStringToWidth(playback, panelWidth - 24),
            left + 12,
            top + 203,
            DgrUiPalette.SECONDARY);
        int bottom = top + geometry.height - 28;
        button(left + 12, bottom, 64, "保存");
        button(left + 86, bottom, 78, "删除音乐");
        button(left + panelWidth - 76, bottom, 64, "关闭");
        UtilityWindowChrome.drawGrip(geometry);
        if (confirming) {
            drawRect(left + 8, top + 80, left + panelWidth - 8, top + 186, DgrUiPalette.WINDOW_PANEL);
            fontRendererObj
                .drawSplitString("确认保存并上传这首本地音乐？完整校验成功后替换旧曲。", left + 20, top + 95, panelWidth - 40, DgrUiPalette.TEXT);
            button(left + 20, top + 148, 130, "确认保存并上传");
            button(left + panelWidth - 90, top + 148, 70, "取消");
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private static String time(double seconds) {
        int value = Math.max(0, (int) seconds);
        return value / 60 + ":" + String.format(java.util.Locale.ROOT, "%02d", value % 60);
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (button != 0) return;
        if (confirming) {
            if (y >= top + 148 && y < top + 168) {
                if (x >= left + 20 && x < left + 150) {
                    confirming = false;
                    try {
                        uploading = true;
                        GramophoneLocalClient.upload(draft.media, request(false), this);
                    } catch (IllegalArgumentException e) {
                        uploading = false;
                        status = e.getMessage();
                    }
                } else if (x >= left + panelWidth - 90 && x < left + panelWidth - 20) confirming = false;
            }
            return;
        }
        int bottom = top + geometry.height - 28;
        if (y >= bottom && y < bottom + 20 && x >= left + panelWidth - 76 && x < left + panelWidth - 12) {
            mc.displayGuiScreen(null);
            return;
        }
        if (uploading) return;
        if (geometry.begin(x, y, button)) return;
        if (!local) source.mouseClicked(x, y, button);
        radius.mouseClicked(x, y, button);
        if (y >= top + 30 && y < top + 50) {
            if (x >= left + 12 && x < left + 122) enabled = !enabled;
            if (x >= left + 132 && x < left + panelWidth - 12) redstone = !redstone;
        }
        if (y >= top + 59 && y < top + 79 && x >= left + 170 && x < left + panelWidth - 12) showRange = !showRange;
        if (y >= top + 87 && y < top + 107) {
            if (x >= left + 12 && x < left + 112) {
                local = !local;
                draft.clear();
                status = local ? "本地 MP3：32 MiB / 15 分钟以内；试听不上传" : "粘贴 QQ / 网易云公开单曲链接";
            }
            if (x >= left + 122 && x < left + panelWidth - 12) {
                if (local) draft.choose();
                else draft.online(source.getText());
            }
        }
        if (y >= top + 139 && y < top + 164) {
            if (x >= left + 12 && x < left + 77) draft.toggle();
            if (x >= left + 86 && x < left + panelWidth - 12) {
                seeking = true;
                draft.beginSeek();
                seek(x);
            }
        }
        if (y >= bottom && y < bottom + 20) {
            if (x >= left + 12 && x < left + 76) save(false);
            if (x >= left + 86 && x < left + 164) save(true);
        }
    }

    private void seek(int x) {
        draft.seek((x - left - 86) / (double) (panelWidth - 98));
    }

    public double[] previewBounds() {
        if (!showRange) return null;
        try {
            int r = Integer.parseInt(radius.getText());
            if (r < 0 || r > GramophoneServer.MAX_RADIUS) return null;
            return new double[] { (double) config.x - r, (double) config.y - r, (double) config.z - r,
                (double) config.x + r + 1, (double) config.y + r + 1, (double) config.z + r + 1 };
        } catch (NumberFormatException ignored) {
            return null;
        }
    }

    private GramophonePacket request(boolean delete) {
        GramophonePacket request = new GramophonePacket();
        request.operation = delete ? GramophonePacket.DELETE : GramophonePacket.SAVE;
        request.x = config.x;
        request.y = config.y;
        request.z = config.z;
        request.dimension = config.dimension;
        request.instance = config.instance;
        request.revision = config.revision;
        try {
            request.radius = Integer.parseInt(radius.getText());
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("范围必须为 0–128 的整数");
        }
        if (request.radius < 0 || request.radius > GramophoneServer.MAX_RADIUS)
            throw new IllegalArgumentException("范围必须为 0–128");
        request.enabled = enabled;
        request.redstone = redstone;
        request.source = delete ? "" : local ? config.source : source.getText();
        if (!local && !request.source.isEmpty()) request.source = OnlineMusicSource.parse(request.source)
            .canonical();
        return request;
    }

    private void save(boolean delete) {
        try {
            GramophonePacket request = request(delete);
            if (local && !delete && draft.media != null) {
                confirming = true;
                return;
            }
            if (local && !delete && !config.source.startsWith("local:"))
                throw new IllegalArgumentException("请先导入并检查本地 MP3");
            GramophoneNetwork.CHANNEL.sendToServer(request);
            status = "正在保存…";
        } catch (IllegalArgumentException e) {
            status = e.getMessage();
        }
    }

    public void transferStatus(String message, boolean complete) {
        status = message;
        if (complete) uploading = false;
    }

    public void result(GramophonePacket packet) {
        if (!packet.key()
            .equals(config.key())) return;
        status = packet.status;
        if (packet.status.equals("已保存")) {
            config = packet;
            if (!packet.source.startsWith("local:")) source.setText(packet.source);
        }
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key == Keyboard.KEY_ESCAPE) {
            if (confirming) confirming = false;
            else mc.displayGuiScreen(null);
            return;
        }
        if (confirming || uploading) return;
        if ((!source.isFocused() || local) && !radius.isFocused()
            && key == mc.gameSettings.keyBindInventory.getKeyCode()) {
            mc.displayGuiScreen(null);
            return;
        }
        if (!local) source.textboxKeyTyped(character, key);
        radius.textboxKeyTyped(character, key);
    }

    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (seeking) {
            seek(x);
            return;
        }
        if (geometry.active()) {
            geometry.move(x, y);
            layout();
        }
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (seeking) draft.endSeek();
        seeking = false;
        if (button == 0 && geometry.active()) {
            geometry.end();
            UtilityWindowChrome.save("gramophone", geometry, width, height);
        }
    }

    @Override
    public void updateScreen() {
        source.updateCursorCounter();
        radius.updateCursorCounter();
        draft.tick();
        if (!local && draft.canonical != null
            && source.getText()
                .equals(draft.onlineInput))
            source.setText(draft.canonical);
    }

    @Override
    public void onGuiClosed() {
        GramophoneLocalClient.cancel(this);
        if (draft != null) draft.close();
        geometry.end();
        if (initialized) UtilityWindowChrome.save("gramophone", geometry, width, height);
        Keyboard.enableRepeatEvents(false);
    }
}
