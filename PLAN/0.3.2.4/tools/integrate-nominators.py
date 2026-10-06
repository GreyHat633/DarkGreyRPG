from pathlib import Path
p=Path('src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java');s=p.read_text(encoding='utf-8')
s=s.replace('    private int panelWidth;', '''    private int panelWidth;
    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(300, 200, 540, 340);
    private boolean geometryInitialized;''')
s=s.replace('''        panelWidth = Math.min(540, width - 12);
        panelHeight = Math.min(340, height - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = (height - panelHeight) / 2;''','''        UtilityWindowChrome.open("entity", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        updateWindowGeometry();''')
a=s.index('    @Override\n    public void updateScreen()')
s=s[:a]+'''    private void updateWindowGeometry() {
        panelLeft = windowGeometry.x;
        panelTop = windowGeometry.y;
        panelWidth = windowGeometry.width;
        panelHeight = windowGeometry.height;
        if (browser != null) browser.layout(panelLeft + 8, panelTop + 24, panelWidth - 16, panelHeight - 108);
        for (Object value : buttonList) {
            GuiButton button = (GuiButton) value;
            button.xPosition = button.id == 1 ? panelLeft + 8 : panelLeft + panelWidth - 84;
            button.yPosition = panelTop + panelHeight - (button.id == 3 ? 78 : 26);
        }
    }

'''+s[a:]
s=s.replace('"实体指名器", width / 2','"实体指名器", panelLeft + panelWidth / 2')
s=s.replace('fontRendererObj.trimStringToWidth(binding, panelWidth - 16)','fontRendererObj.trimStringToWidth(binding, panelWidth - 108)').replace('panelTop + panelHeight - 51','panelTop + panelHeight - 71')
s=s.replace('        super.drawScreen(mx, my, partial);','        UtilityWindowChrome.drawGrip(windowGeometry);\n        super.drawScreen(mx, my, partial);')
s=s.replace('        browser.click(x, y, b);','        if (windowGeometry.begin(x, y, b)) return;\n        browser.click(x, y, b);')
s=s.replace('if (!controls.modal() && !controls.pending) browser.scroll(', 'if (!controls.modal() && !controls.pending && !windowGeometry.active()) browser.scroll(')
a=s.index('    @Override\n    public boolean doesGuiPauseGame()')
s=s[:a]+'''    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (windowGeometry.active()) {
            if (controls.modal() || controls.pending) { windowGeometry.end(); return; }
            windowGeometry.move(x, y);
            updateWindowGeometry();
            return;
        }
        super.mouseClickMove(x, y, button, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("entity", windowGeometry, width, height);
            return;
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    public void onGuiClosed() {
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("entity", windowGeometry, width, height);
        super.onGuiClosed();
    }

'''+s[a:];p.write_text(s,encoding='utf-8')
p=Path('src/main/java/darkgrey/rpg/client/gui/GuiNominatorInventory.java');s=p.read_text(encoding='utf-8')
s=s.replace('    private GuiButton bindButton;', '''    private GuiButton bindButton;
    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(308, 240, 420, 350);
    private boolean geometryInitialized;''')
s=s.replace('ySize - 160','ySize - 184')
s=s.replace('''        xSize = Math.min(420, width - 12);
        ySize = Math.min(350, height - 12);
        super.initGui();
        buttonList.clear();''','''        UtilityWindowChrome.open("item", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        xSize = windowGeometry.width;
        ySize = windowGeometry.height;
        super.initGui();
        buttonList.clear();
        layoutControls();
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(7, guiLeft + xSize - 90, guiTop + ySize - 156, 82, 20, "ID释放"));
        bindButton = new GuiRpgButton(6, guiLeft + operationLeft, guiTop + ySize - 90, 82, 18, "物品指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(8, guiLeft + operationLeft, guiTop + ySize - 36, 82, 18, "物品解绑"));
    }

    private void layoutControls() {
        guiLeft = windowGeometry.x;
        guiTop = windowGeometry.y;
        xSize = windowGeometry.width;
        ySize = windowGeometry.height;''')
a=s.index('        rebuildBrowser();\n        // The browser ends');b=s.index('\n    @Override\n    public void updateScreen()',a)
s=s[:a]+'''        if (browser != null) browser.layout(guiLeft + 8, guiTop + 24, xSize - 16, ySize - 184);
        for (Object value : buttonList) {
            GuiButton button = (GuiButton) value;
            button.xPosition = guiLeft + (button.id == 7 ? xSize - 90 : operationLeft);
            button.yPosition = guiTop + ySize - (button.id == 7 ? 156 : button.id == 6 ? 90 : 36);
        }
    }
'''+s[b:]
s=s.replace('''            mc.displayGuiScreen(null);
            return;''','''            super.keyTyped(c, key);
            return;''')
s=s.replace('''        browser.click(x, y, b);
        super.mouseClicked(x, y, b);''','''        if (slotAt(x, y)) {
            browser.search.setFocused(false);
            super.mouseClicked(x, y, b);
            return;
        }
        if (mc.thePlayer.inventory.getItemStack() == null && windowGeometry.begin(x, y, b)) return;
        browser.click(x, y, b);
        super.mouseClicked(x, y, b);''')
s=s.replace('''        if (!controls.modal() && !controls.pending) super.mouseClickMove(x, y, b, elapsed);''','''        if (windowGeometry.active()) {
            if (controls.modal() || controls.pending) { windowGeometry.end(); return; }
            windowGeometry.move(x, y);
            layoutControls();
            return;
        }
        if (!controls.modal() && !controls.pending) super.mouseClickMove(x, y, b, elapsed);''')
s=s.replace('''        if (!controls.modal() && !controls.pending) super.mouseMovedOrUp(x, y, b);''','''        if (b == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("item", windowGeometry, width, height);
            return;
        }
        if (!controls.modal() && !controls.pending) super.mouseMovedOrUp(x, y, b);''')
s=s.replace('if (!controls.modal() && !controls.pending) browser.scroll(', 'if (!controls.modal() && !controls.pending && !windowGeometry.active()) browser.scroll(')
s=s.replace('        controls.draw(width, height, mx, my);','        UtilityWindowChrome.drawGrip(windowGeometry);\n        controls.draw(width, height, mx, my);')
a=s.index('    @Override\n    public boolean doesGuiPauseGame()')
s=s[:a]+'''    private boolean slotAt(int x, int y) {
        for (Object value : inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) value;
            int left = guiLeft + slot.xDisplayPosition, top = guiTop + slot.yDisplayPosition;
            if (x >= left - 1 && x < left + 17 && y >= top - 1 && y < top + 17) return true;
        }
        return false;
    }

    @Override
    public void onGuiClosed() {
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("item", windowGeometry, width, height);
        super.onGuiClosed();
    }

'''+s[a:];p.write_text(s,encoding='utf-8')
