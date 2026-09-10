package darkgrey.rpg.client.gui;

import java.util.UUID;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorAction;

/** Small correlated operation/modal state shared by the two Nominator views. */
public final class NominatorControls extends Gui {

    private final String token = UUID.randomUUID()
        .toString();
    private int sequence;
    public boolean pending;
    public boolean initialized;
    public long revision = -1, catalogRevision = -1, npcRevision = -1;
    public String message = "";
    private String modal = "";
    private NBTTagCompound request;
    private GuiRpgButton left, right;

    public boolean modal() {
        return !modal.isEmpty();
    }

    public void cancel() {
        modal = "";
        request = null;
    }

    public void begin(NBTTagCompound q, String mode) {
        request = (NBTTagCompound) q.copy();
        if ("release".equals(mode)) modal = "release";
        else if ("group".equals(mode)) modal = "group";
        else send();
    }

    private void send() {
        request.setString("token", token);
        request.setInteger("sequence", ++sequence);
        request.setLong("revision", revision);
        request.setLong("catalogRevision", catalogRevision);
        request.setLong("npcRevision", npcRevision);
        pending = true;
        modal = "";
        DialogueNetwork.CHANNEL.sendToServer(new C2SNominatorAction(request));
    }

    public boolean accept(NBTTagCompound data) {
        if (!pending || !token.equals(data.getString("token")) || sequence != data.getInteger("sequence")) return false;
        pending = false;
        initialized = true;
        revision = data.getLong("revision");
        catalogRevision = data.getLong("catalogRevision");
        npcRevision = data.getLong("npcRevision");
        message = data.getString("message");
        if ("transfer_required".equals(data.getString("code"))) modal = "transfer";
        return true;
    }

    public void draw(int width, int height, int mx, int my) {
        if (!modal()) return;
        Minecraft mc = Minecraft.getMinecraft();
        FontRenderer font = mc.fontRenderer;
        int w = Math.min(300, width - 16), h = 110, x = (width - w) / 2, y = (height - h) / 2;
        drawRect(0, 0, width, height, DgrUiPalette.MODAL_MASK);
        drawRect(x, y, x + w, y + h, DgrUiPalette.SELECTED_BORDER);
        drawRect(x + 1, y + 1, x + w - 1, y + h - 1, DgrUiPalette.SUB_PANEL);
        String title = "group".equals(modal) ? "匹配方式"
            : "transfer".equals(modal) ? ("NPC".equals(request.getString("type")) ? "NPCID 已被占用" : "ItemID 已被占用")
                : "ID释放";
        font.drawString(title, x + 10, y + 10, DgrUiPalette.TEXT);
        font.drawString(
            font.trimStringToWidth(request.getString("resource"), w - 20),
            x + 10,
            y + 29,
            DgrUiPalette.SECONDARY);
        String text = "group".equals(modal) ? "该物品以哪种方式加入此物品组？"
            : "transfer".equals(modal) ? "是否转移到当前宿主？" : "清除该资源的全部世界绑定，保留资源？";
        font.drawString(font.trimStringToWidth(text, w - 20), x + 10, y + 47, DgrUiPalette.TEXT);
        int bw = (w - 30) / 2;
        left = new GuiRpgButton(
            90,
            x + 10,
            y + 77,
            bw,
            22,
            "group".equals(modal) ? "精准匹配" : "transfer".equals(modal) ? "转移" : "ID释放");
        right = new GuiRpgButton(91, x + 20 + bw, y + 77, bw, 22, "group".equals(modal) ? "模糊匹配" : "取消");
        left.drawButton(mc, mx, my);
        right.drawButton(mc, mx, my);
    }

    public void click(int x, int y, int button) {
        if (!modal() || button != 0 || left == null || right == null) return;
        boolean yes = left.mousePressed(Minecraft.getMinecraft(), x, y),
            no = right.mousePressed(Minecraft.getMinecraft(), x, y);
        if (!yes && !no) return;
        if ("group".equals(modal)) {
            request.setString("mode", yes ? "EXACT" : "FUZZY");
            send();
        } else if (yes) {
            if ("transfer".equals(modal)) request.setString("op", "transfer");
            send();
        } else cancel();
    }
}
