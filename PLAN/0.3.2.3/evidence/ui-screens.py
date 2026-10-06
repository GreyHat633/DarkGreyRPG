from pathlib import Path
B=Path('src/main/java/darkgrey/rpg/client/gui')
p=B/'GuiNominatorInventory.java';s=p.read_text(encoding='utf8')
a=s.index('    @Override\n    public void initGui()');s=s[:a]+'''    private final NominatorControls controls=new NominatorControls();
    private int inventoryLeft;

    public void acceptResult(net.minecraft.nbt.NBTTagCompound data,NominatorCatalog catalog) {
        if (!controls.accept(data)) return;
        this.catalog=catalog;
        revision=controls.revision; catalogRevision=controls.catalogRevision;
        rebuildBrowser();
    }

    private void rebuildBrowser() {
        NominatorBrowser old=browser;
        browser=new NominatorBrowser(fontRendererObj,catalog,true,guiLeft+8,guiTop+24,xSize-16,ySize-160);
        browser.restore(old);
    }

    @Override
    public void initGui() {
        xSize=Math.min(420,width-12); ySize=Math.min(350,height-12);
        super.initGui(); buttonList.clear();
        inventoryLeft=Math.max(8,(xSize-104-162)/2);
        for (int i=0;i<inventorySlots.inventorySlots.size();i++) {
            net.minecraft.inventory.Slot slot=(net.minecraft.inventory.Slot)inventorySlots.inventorySlots.get(i);
            if (i<ContainerNominatorInventory.PLAYER_SLOT_START) {
                slot.xDisplayPosition=xSize-54;
                slot.yDisplayPosition=ySize-114+i*58;
            } else {
                int j=i-ContainerNominatorInventory.PLAYER_SLOT_START;
                slot.xDisplayPosition=inventoryLeft+(j%9)*18;
                slot.yDisplayPosition=ySize-109+(j<27?(j/9)*18:58);
            }
        }
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(7,guiLeft+8,guiTop+ySize-132,76,20,"ID释放"));
        bindButton=new GuiRpgButton(6,guiLeft+xSize-90,guiTop+ySize-94,82,18,"指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(8,guiLeft+xSize-90,guiTop+ySize-36,82,18,"物品解绑"));
    }

    @Override
    public void updateScreen() {
        super.updateScreen();
        if (!controls.initialized&&!controls.pending) send("sync");
    }

    private void send(String op) {
        net.minecraft.nbt.NBTTagCompound q=new net.minecraft.nbt.NBTTagCompound();
        q.setBoolean("items",true); q.setInteger("window",inventorySlots.windowId); q.setString("op",op);
        darkgrey.rpg.client.NominatorGlobalSearch.Row row=browser.selected();
        if (row!=null) { q.setString("resource",row.id);q.setString("type",row.type);q.setString("package",row.source.getPackageId()); }
        controls.begin(q,"release".equals(op)?"release":"bind".equals(op)&&row!=null&&"Item Group".equals(row.type)?"group":"");
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (controls.pending||controls.modal()||!controls.initialized) return;
        if (button.id==6&&browser.selected()!=null&&hasTarget(ContainerNominatorInventory.NOMINATE_SLOT)) send("bind");
        if (button.id==7&&browser.selected()!=null) send("release");
        if (button.id==8&&hasTarget(ContainerNominatorInventory.UNBIND_SLOT)) send("unbind");
    }

    private boolean hasTarget(int slot) {
        return ((ContainerNominatorInventory)inventorySlots).getTargetInventory().getStackInSlot(slot)!=null;
    }

    @Override
    public void drawScreen(int mx,int my,float partial) {
        for (Object obj:buttonList) {
            GuiButton button=(GuiButton)obj;
            button.enabled=controls.initialized&&!controls.pending&&!controls.modal() && (button.id==8?hasTarget(ContainerNominatorInventory.UNBIND_SLOT):browser.selected()!=null);
            if (button.id==6) button.enabled &= hasTarget(ContainerNominatorInventory.NOMINATE_SLOT);
        }
        super.drawScreen(mx,my,partial);
        org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
        controls.draw(width,height,mx,my);
        org.lwjgl.opengl.GL11.glEnable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
    }

    @Override
    protected void drawGuiContainerBackgroundLayer(float partial,int mx,int my) {
        drawRect(guiLeft,guiTop,guiLeft+xSize,guiTop+ySize,DgrUiPalette.PANEL);
        browser.draw(mx,my);
        drawRect(guiLeft+inventoryLeft-3,guiTop+ySize-112,guiLeft+inventoryLeft+165,guiTop+ySize-31,DgrUiPalette.SUB_PANEL);
        drawRect(guiLeft+xSize-96,guiTop+ySize-130,guiLeft+xSize-5,guiTop+ySize-15,DgrUiPalette.BORDER);
        drawRect(guiLeft+xSize-95,guiTop+ySize-129,guiLeft+xSize-6,guiTop+ySize-16,DgrUiPalette.SUB_PANEL);
        for (Object obj:inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot=(net.minecraft.inventory.Slot)obj;
            int x=guiLeft+slot.xDisplayPosition,y=guiTop+slot.yDisplayPosition;
            drawRect(x-1,y-1,x+17,y+17,DgrUiPalette.SLOT_BORDER);
            drawRect(x,y,x+16,y+16,0xFF666666);
        }
    }

    @Override
    protected void drawGuiContainerForegroundLayer(int mx,int my) {
        fontRendererObj.drawString("物品指名器",8,8,DgrUiPalette.TEXT);
        fontRendererObj.drawString("玩家背包",inventoryLeft,ySize-123,DgrUiPalette.SECONDARY);
        fontRendererObj.drawString("物品指名",xSize-86,ySize-126,DgrUiPalette.TEXT);
        fontRendererObj.drawString("物品解绑",xSize-86,ySize-68,DgrUiPalette.TEXT);
        fontRendererObj.drawString(fontRendererObj.trimStringToWidth(controls.message,xSize-16),8,ySize-12,DgrUiPalette.SECONDARY);
    }

    @Override
    protected void keyTyped(char c,int key) {
        if (controls.modal()) { if (key==1) controls.cancel();return; }
        if (controls.pending && key!=1) return;
        if (key==1 || key==mc.gameSettings.keyBindInventory.getKeyCode()&&!browser.search.isFocused()) { super.keyTyped(c,key);return; }
        if (!browser.search.textboxKeyTyped(c,key)) super.keyTyped(c,key);
    }
    @Override
    protected void mouseClicked(int x,int y,int b) {
        if (controls.modal()) { controls.click(x,y,b);return; }
        if (controls.pending) return;
        browser.click(x,y,b); super.mouseClicked(x,y,b);
    }
    @Override
    protected void mouseClickMove(int x,int y,int b,long elapsed) {
        if (!controls.modal()&&!controls.pending) super.mouseClickMove(x,y,b,elapsed);
    }
    @Override
    protected void mouseMovedOrUp(int x,int y,int b) {
        if (!controls.modal()&&!controls.pending) super.mouseMovedOrUp(x,y,b);
    }
    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        if (!controls.modal()&&!controls.pending) browser.scroll(org.lwjgl.input.Mouse.getEventX()*width/mc.displayWidth,
            height-org.lwjgl.input.Mouse.getEventY()*height/mc.displayHeight-1,org.lwjgl.input.Mouse.getEventDWheel());
    }
    @Override
    public boolean doesGuiPauseGame() { return false; }
}
'''
s=s.replace('        if (mc != null) initGui();','        if (mc != null) rebuildBrowser();')
p.write_text(s,encoding='utf8')
p=B/'GuiNominatorEntity.java';s=p.read_text(encoding='utf8');a=s.index('    @Override\n    public void initGui()');s=s[:a]+'''    private final NominatorControls controls=new NominatorControls();
    private String currentGroups="";
    private int panelHeight;
    public void acceptResult(net.minecraft.nbt.NBTTagCompound data,NominatorCatalog catalog) {
        if (!controls.accept(data)) return;
        this.catalog=catalog; individual=data.getString("individual"); currentGroups=data.getString("groups");
        revision=controls.revision; catalogRevision=controls.catalogRevision; rebuildBrowser();
    }
    private void rebuildBrowser() {
        NominatorBrowser old=browser;
        browser=new NominatorBrowser(fontRendererObj,catalog,false,panelLeft+8,panelTop+24,panelWidth-16,panelHeight-108);
        browser.restore(old);
    }
    @Override
    public void initGui() {
        buttonList.clear();panelWidth=Math.min(540,width-12);panelHeight=Math.min(340,height-12);
        panelLeft=(width-panelWidth)/2;panelTop=(height-panelHeight)/2;
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(3,panelLeft+8,panelTop+panelHeight-78,76,20,"ID释放"));
        bindButton=new GuiRpgButton(1,panelLeft+panelWidth-90,panelTop+panelHeight-78,82,20,"实体指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(2,panelLeft+8,panelTop+panelHeight-26,76,20,"实体解绑"));
        buttonList.add(new GuiRpgButton(0,panelLeft+panelWidth-84,panelTop+panelHeight-26,76,20,"关闭"));
    }
    @Override
    public void updateScreen() {
        super.updateScreen();if (!controls.initialized&&!controls.pending) send("sync");
    }
    private void send(String op) {
        net.minecraft.nbt.NBTTagCompound q=new net.minecraft.nbt.NBTTagCompound();
        q.setString("op",op);q.setInteger("entity",entityId);q.setString("entityUuid",entityUuid.toString());
        darkgrey.rpg.client.NominatorGlobalSearch.Row row=browser.selected();
        if (row!=null) { q.setString("resource",row.id);q.setString("type",row.type);q.setString("package",row.source.getPackageId()); }
        controls.begin(q,"release".equals(op)?"release":"");
    }
    @Override
    protected void actionPerformed(GuiButton button) {
        if (controls.modal()||controls.pending) return;
        if (button.id==0) { mc.displayGuiScreen(null);return; }
        if (!controls.initialized) return;
        if (button.id==2) send("unbind");
        if (browser.selected()!=null) {
            if (button.id==1) send("bind");
            if (button.id==3) send("release");
        }
    }
    @Override
    public void drawScreen(int mx,int my,float partial) {
        drawDefaultBackground();
        drawRect(panelLeft,panelTop,panelLeft+panelWidth,panelTop+panelHeight,DgrUiPalette.PANEL);
        drawCenteredString(fontRendererObj,"实体指名器",width/2,panelTop+8,DgrUiPalette.TEXT);
        browser.draw(mx,my);
        String binding="当前实体："+(individual==null?"":individual)+" "+currentGroups;
        drawString(fontRendererObj,fontRendererObj.trimStringToWidth(binding,panelWidth-16),panelLeft+8,panelTop+panelHeight-51,DgrUiPalette.SECONDARY);
        drawString(fontRendererObj,fontRendererObj.trimStringToWidth(controls.message,panelWidth-16),panelLeft+8,panelTop+panelHeight-40,DgrUiPalette.SECONDARY);
        for (Object obj:buttonList) {
            GuiButton b=(GuiButton)obj;
            b.enabled=!controls.pending&&!controls.modal()&&(b.id==0||controls.initialized)&&(b.id!=1&&b.id!=3||browser.selected()!=null);
        }
        super.drawScreen(mx,my,partial);controls.draw(width,height,mx,my);
    }
    @Override
    protected void keyTyped(char c,int key) {
        if (controls.modal()) { if (key==1) controls.cancel();return; }
        if (controls.pending&&key!=1) return;
        if (key==1||!browser.search.textboxKeyTyped(c,key)) super.keyTyped(c,key);
    }
    @Override
    protected void mouseClicked(int x,int y,int b) {
        if (controls.modal()) { controls.click(x,y,b);return; }
        if (controls.pending) return;
        browser.click(x,y,b);super.mouseClicked(x,y,b);
    }
    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        if (!controls.modal()&&!controls.pending) browser.scroll(org.lwjgl.input.Mouse.getEventX()*width/mc.displayWidth,
            height-org.lwjgl.input.Mouse.getEventY()*height/mc.displayHeight-1,org.lwjgl.input.Mouse.getEventDWheel());
    }
    @Override
    public boolean doesGuiPauseGame() { return false; }
}
''';p.write_text(s,encoding='utf8')
p=Path('src/main/java/darkgrey/rpg/nominator/container/ContainerNominatorInventory.java');s=p.read_text(encoding='utf8').replace('if (player != owner || player.worldObj == null || player.worldObj.isRemote) return;', 'if (player != owner) return;');p.write_text(s,encoding='utf8')
p=Path('src/main/java/darkgrey/rpg/nominator/container/InventoryNominatorTarget.java');s=p.read_text(encoding='utf8').replace('The transient, one-stacks[slot] target inventory','The transient two-slot inventory');p.write_text(s,encoding='utf8')
p=Path('src/test/java/darkgrey/rpg/creator/CreatorNetworkDiscriminatorProbe.java');s=p.read_text(encoding='utf8').replace('ids.size() != 16','ids.size() != 18').replace('Expected 16 registrations','Expected 18 registrations').replace('i <= 18','i <= 20');p.write_text(s,encoding='utf8')
p=Path('src/test/java/darkgrey/rpg/nominator/container/NominatorItemContainerProbe.java');s=p.read_text(encoding='utf8').replace('size() == 37','size() == 38').replace('one target plus 36','two targets plus 36').replace('player, 29','player, 30').replace('player, 28','player, 29');p.write_text(s,encoding='utf8')
