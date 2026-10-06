from pathlib import Path
p=Path('src/main/java/darkgrey/rpg/client/gui/GuiCopierTemplates.java')
s=p.read_text(encoding='utf-8')
s=s.replace('private int panelLeft, panelTop, panelWidth, panelHeight;', '''private int panelLeft, panelTop, panelWidth, panelHeight;
    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(260, 160, 420, 340);
    private boolean geometryInitialized;
    private int laidOutOffset = -1;
    private int laidOutDelete = -2;''')
a=s.index('        panelWidth = Math.min(420, width - 12);');b=s.index('        int rows =',a)
s=s[:a]+'''        UtilityWindowChrome.open("copier", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        layoutControls();
    }

    private void layoutControls() {
        panelLeft = windowGeometry.x;
        panelTop = windowGeometry.y;
        panelWidth = windowGeometry.width;
        panelHeight = windowGeometry.height;
        offset = Math.max(0, Math.min(offset, maxOffset()));
'''+s[b:]
a=s.index('        for (int row = 0; row < rows; row++) {');
s=s[:a]+'''        if (buttonList.size() == rows * 2 + 1 && laidOutOffset == offset && laidOutDelete == pendingDelete) {
            for (int row = 0; row < rows; row++) {
                GuiButton select = (GuiButton) buttonList.get(row * 2);
                GuiButton delete = (GuiButton) buttonList.get(row * 2 + 1);
                select.xPosition = panelLeft + 8;
                select.yPosition = listTop() + row * ROW_HEIGHT;
                select.width = rowButtonWidth;
                String prefix = offset + row == selectedIndex ? "✓ " : "  ";
                select.displayString = fontRendererObj.trimStringToWidth(prefix + (offset + row + 1) + ". "
                    + templates.get(offset + row).getEntityType(), rowButtonWidth - 12);
                delete.xPosition = panelLeft + panelWidth - 72;
                delete.yPosition = select.yPosition;
            }
            GuiButton close = (GuiButton) buttonList.get(rows * 2);
            close.xPosition = panelLeft + panelWidth - 88;
            close.yPosition = panelTop + panelHeight - 28;
            return;
        }
        buttonList.clear();
        laidOutOffset = offset;
        laidOutDelete = pendingDelete;
'''+s[a:]
s=s.replace('                initGui();','                layoutControls();').replace('        initGui();','        layoutControls();')
s=s.replace('            width / 2,','            panelLeft + panelWidth / 2,')
s=s.replace('        super.drawScreen(mouseX, mouseY, partialTicks);','        UtilityWindowChrome.drawGrip(windowGeometry);\n        super.drawScreen(mouseX, mouseY, partialTicks);')
s=s.replace('        super.handleMouseInput();','        super.handleMouseInput();\n        if (windowGeometry.active() || pendingDelete >= 0) return;')
a=s.index('    @Override\n    public boolean doesGuiPauseGame()')
s=s[:a]+'''    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (pendingDelete < 0 && windowGeometry.begin(x, y, button)) return;
        super.mouseClicked(x, y, button);
    }

    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (windowGeometry.active()) {
            windowGeometry.move(x, y);
            layoutControls();
            return;
        }
        super.mouseClickMove(x, y, button, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("copier", windowGeometry, width, height);
            return;
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key == 1 && pendingDelete >= 0) {
            pendingDelete = -1;
            layoutControls();
            return;
        }
        super.keyTyped(character, key);
    }

    @Override
    public void onGuiClosed() {
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("copier", windowGeometry, width, height);
        super.onGuiClosed();
    }

'''+s[a:]
p.write_text(s,encoding='utf-8')
