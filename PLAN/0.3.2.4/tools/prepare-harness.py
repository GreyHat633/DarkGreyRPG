from pathlib import Path
p=Path('.tooling/0.3.2.4/live-src/dgr/acceptance/LiveHarness.java')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('"packageScroll","resourceScroll",','"packageMotion","resourceMotion",')
s=s.replace('if(op.equals("mousefields"))', '''if(op.equals("remapinventory")) {mc.gameSettings.keyBindInventory.setKeyCode(r.get("key").getAsInt());net.minecraft.client.settings.KeyBinding.resetKeyBindingArrayAndHash();}
  if(op.equals("typedkey")) {Method m=GuiScreen.class.getDeclaredMethod("keyTyped",char.class,int.class);m.setAccessible(true);m.invoke(mc.currentScreen,r.get("text").getAsString().charAt(0),r.get("key").getAsInt());}
  if(op.equals("down") || op.equals("move") || op.equals("up")) {
   String method=op.equals("down")?"mouseClicked":op.equals("move")?"mouseClickMove":"mouseMovedOrUp";
   Method m=op.equals("move")?GuiScreen.class.getDeclaredMethod(method,int.class,int.class,int.class,long.class):GuiScreen.class.getDeclaredMethod(method,int.class,int.class,int.class);
   m.setAccessible(true);int x=r.get("x").getAsInt(),y=r.get("y").getAsInt();
   if(op.equals("move"))m.invoke(mc.currentScreen,x,y,0,40L);else m.invoke(mc.currentScreen,x,y,0);
  }
  if(op.equals("mousefields"))''')
s=s.replace('out.put("audit",audit);','''out.put("audit",audit);
  out.put("fps",Minecraft.debugFPS);
  if(mc.currentScreen!=null) {try {out.put("windowGeometry",field(mc.currentScreen,"windowGeometry"));}catch(Exception ignored){}}
''')
p.write_text(s,encoding='utf-8')
