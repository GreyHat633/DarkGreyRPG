from pathlib import Path
R=Path.cwd(); B=R/'src/main/java/darkgrey/rpg/client/gui'
def put(n,s):(B/n).write_text(s,encoding='utf8')
put('DgrUiPalette.java','''package darkgrey.rpg.client.gui;

/** Shared visual constants only. */
public final class DgrUiPalette {
    private DgrUiPalette() {}
    public static final int PANEL=0xEE303030;
    public static final int SUB_PANEL=0xFF202020;
    public static final int BORDER=0xFF888888;
    public static final int SELECTED_BORDER=0xFFDDDDDD;
    public static final int HOVER=0xFF505050;
    public static final int TEXT=0xFFE8E8E8;
    public static final int SECONDARY=0xFFAAAAAA;
    public static final int DISABLED=0xFF777777;
    public static final int SLOT_BORDER=0xFFCCCCCC;
    public static final int MODAL_MASK=0xB0000000;
}
''')
put('NominatorControls.java','''package darkgrey.rpg.client.gui;

import java.util.UUID;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.nbt.NBTTagCompound;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorAction;

/** Small correlated operation/modal state shared by the two Nominator views. */
public final class NominatorControls extends Gui {
    private final String token=UUID.randomUUID().toString();
    private int sequence;
    public boolean pending;
    public boolean initialized;
    public long revision=-1, catalogRevision=-1, npcRevision=-1;
    public String message="";
    private String modal="";
    private NBTTagCompound request;
    private GuiRpgButton left,right;
    public boolean modal() { return !modal.isEmpty(); }
    public void cancel() { modal=""; request=null; }
    public void begin(NBTTagCompound q,String mode) {
        request=(NBTTagCompound)q.copy();
        if ("release".equals(mode)) modal="release";
        else if ("group".equals(mode)) modal="group";
        else send();
    }
    private void send() {
        request.setString("token",token);
        request.setInteger("sequence",++sequence);
        request.setLong("revision",revision);
        request.setLong("catalogRevision",catalogRevision);
        request.setLong("npcRevision",npcRevision);
        pending=true; modal="";
        DialogueNetwork.CHANNEL.sendToServer(new C2SNominatorAction(request));
    }
    public boolean accept(NBTTagCompound data) {
        if (!pending || !token.equals(data.getString("token")) || sequence!=data.getInteger("sequence")) return false;
        pending=false; initialized=true;
        revision=data.getLong("revision"); catalogRevision=data.getLong("catalogRevision"); npcRevision=data.getLong("npcRevision");
        message=data.getString("message");
        if ("transfer_required".equals(data.getString("code"))) modal="transfer";
        return true;
    }
    public void draw(int width,int height,int mx,int my) {
        if (!modal()) return;
        Minecraft mc=Minecraft.getMinecraft(); FontRenderer font=mc.fontRenderer;
        int w=Math.min(300,width-16), h=110, x=(width-w)/2,y=(height-h)/2;
        drawRect(0,0,width,height,DgrUiPalette.MODAL_MASK);
        drawRect(x,y,x+w,y+h,DgrUiPalette.SELECTED_BORDER);
        drawRect(x+1,y+1,x+w-1,y+h-1,DgrUiPalette.SUB_PANEL);
        String title="group".equals(modal)?"匹配方式":"transfer".equals(modal)?("NPC".equals(request.getString("type"))?"NPCID 已被占用":"ItemID 已被占用"):"ID释放";
        font.drawString(title,x+10,y+10,DgrUiPalette.TEXT);
        font.drawString(font.trimStringToWidth(request.getString("resource"),w-20),x+10,y+29,DgrUiPalette.SECONDARY);
        String text="group".equals(modal)?"该物品以哪种方式加入此物品组？":"transfer".equals(modal)?"是否转移到当前宿主？":"清除该资源的全部世界绑定，保留资源？";
        font.drawString(font.trimStringToWidth(text,w-20),x+10,y+47,DgrUiPalette.TEXT);
        int bw=(w-30)/2;
        left=new GuiRpgButton(90,x+10,y+77,bw,22,"group".equals(modal)?"精准匹配":"transfer".equals(modal)?"转移":"ID释放");
        right=new GuiRpgButton(91,x+20+bw,y+77,bw,22,"group".equals(modal)?"模糊匹配":"取消");
        left.drawButton(mc,mx,my); right.drawButton(mc,mx,my);
    }
    public void click(int x,int y,int button) {
        if (!modal() || button!=0 || left==null || right==null) return;
        boolean yes=left.mousePressed(Minecraft.getMinecraft(),x,y), no=right.mousePressed(Minecraft.getMinecraft(),x,y);
        if (!yes&&!no) return;
        if ("group".equals(modal)) { request.setString("mode",yes?"EXACT":"FUZZY"); send(); }
        else if (yes) { if ("transfer".equals(modal)) request.setString("op","transfer"); send(); }
        else cancel();
    }
}
''')
# Preserve browser state by copying into resized/refreshed instances; one-line rows.
p=B/'NominatorBrowser.java';s=p.read_text()
s=s.replace('    private int count() {', '''    public void restore(NominatorBrowser old) {
        if (old==null) return;
        query=old.search.getText(); search.setText(query);
        if (catalog.getPackageChoice(old.selectedPackage)!=null) selectedPackage=old.selectedPackage;
        refresh();
        packageScroll=Math.max(0,Math.min(old.packageScroll,Math.max(0,catalog.getPackageChoices().size()-count())));
        resourceScroll=Math.max(0,Math.min(old.resourceScroll,Math.max(0,rows.size()-count())));
        if (old.selected!=null) for (NominatorGlobalSearch.Row row:rows)
            if (row.id.equals(old.selected.id)&&row.type.equals(old.selected.type)&&row.source.getPackageId().equals(old.selected.source.getPackageId())) selected=row;
    }

    public static String resourceLabel(darkgrey.rpg.client.NominatorGlobalSearch.Row row, boolean global) {
        String type="NPC".equals(row.type)?"NPCID":"Item".equals(row.type)?"ItemID":"GroupID";
        return (global?"["+row.source.getDisplayName()+"] ":"")+row.name+"  "+net.minecraft.util.EnumChatFormatting.GRAY+"["+type+"] "+row.id;
    }

    private int count() {''')
s=s.replace('/ 32','/ 20').replace('* 32','* 20').replace('top + 30','top + 18')
s=s.replace('top + 3, 0xFFFFFF','top + 5, DgrUiPalette.TEXT')
s=s.replace('                font.drawString(font.trimStringToWidth(p.getPackageId(), split - 8), x + 4, top + 15, 0xAAAAAA);','')
a=s.index('                font.drawString(font.trimStringToWidth("[" + r.type');b=s.index('\n            }',a)
s=s[:a]+'''                font.drawString(font.trimStringToWidth(resourceLabel(r,!query.trim().isEmpty()),rw),rx,top+5,DgrUiPalette.TEXT);'''+s[b:]
p.write_text(s,encoding='utf8')
# Replace reviewed warm color values with shared gray tokens; geometry/input remain unchanged.
files=['GuiRpgButton.java','GuiCanonicalTaskScreen.java','GuiCopierTemplates.java','GuiQuestJournal.java']
import re
mapping={'FF55514A':'DISABLED','FFE4D5AE':'SELECTED_BORDER','FF958976':'BORDER','EE40392E':'HOVER','DD181818':'SUB_PANEL','888078':'DISABLED','FFF0CD':'TEXT','E4D5AE':'TEXT','FFF0E6D2':'TEXT','FFFFC46B':'TEXT','FF766D58':'BORDER','FF514A3B':'BORDER','FF574C32':'HOVER','FF343126':'HOVER','FFFFE8A8':'TEXT','FFE4E0D5':'TEXT','FFBDB4A0':'SECONDARY','F02B2F4A':'PANEL','FF7D8CFF':'BORDER','CC1E213A':'SUB_PANEL','FFEEF0FF':'TEXT','AAEEF0FF':'SECONDARY'}
for n in files:
 p=B/n;s=p.read_text()
 for v,k in mapping.items():s=re.sub(r'0x'+v+r'\b','DgrUiPalette.'+k,s)
 p.write_text(s,encoding='utf8')
p=B.parent/'CreatorInspectClient.java';s=p.read_text().replace('0xEEDDCC','darkgrey.rpg.client.gui.DgrUiPalette.TEXT');p.write_text(s,encoding='utf8')
