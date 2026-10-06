from pathlib import Path
import shutil
R=Path.cwd();old=R/'.tooling/0.3.2.2';new=R/'.tooling/0.3.2.3';client=new/'live-client';client.mkdir(exist_ok=True)
for n in ['config','darkgrey_rpg_project','darkgrey_rpg_story_packages']:
 shutil.copytree(old/'live-client'/n,client/n,dirs_exist_ok=True)
(client/'mods').mkdir(exist_ok=True)
for n in ['+unimixins-all-1.7.10-0.3.1.jar','CustomNPC-Plus-1.11.1-fixed-v1.jar']:shutil.copy2(old/'live-client/mods'/n,client/'mods'/n)
shutil.copy2(old/'live-client/options.txt',client/'options.txt')
(new/'live-src/dgr/acceptance').mkdir(parents=True,exist_ok=True)
s=(old/'live-src/dgr/acceptance/LiveHarness.java').read_text(encoding='utf8').replace('0322','0323').replace('0.3.2.2','0.3.2.3').replace('32261','32361')
s=s.replace(' static final Gson GSON', ' static volatile Map<String,Object> audit=new LinkedHashMap<String,Object>();\n static Entity hostA,hostB;\n static final Gson GSON')
s=s.replace('  if(op.equals("start"))', '''  if(op.equals("audit0323")||op.equals("fixture0323")||op.equals("removeA")||op.equals("seedgroups")||op.equals("items0323")) {
   MainThreadScheduler.scheduleServer(new Runnable(){public void run(){try{server0323(r);}catch(Exception e){audit=Collections.<String,Object>singletonMap("error",e.toString());e.printStackTrace();}}});
  }
  if(op.equals("openhost")) {
   Entity e=r.get("host").getAsString().equals("A")?hostA:hostB;
   DialogueNetwork.CHANNEL.sendToServer(new darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen(e.getEntityId()));
  }
  if(op.equals("start"))''')
s=s.replace('  out.put("taskRevision",', '''  out.put("audit",audit);
  if(mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiNominatorInventory || mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiNominatorEntity) {
   Object browser=field(mc.currentScreen,"browser"),controls=field(mc.currentScreen,"controls");
   Map<String,Object> ui=new LinkedHashMap<String,Object>();
   for(String n:new String[]{"pending","initialized","revision","catalogRevision","npcRevision","message","modal","left","right"})ui.put(n,field(controls,n));
   ui.put("search",((GuiTextField)field(browser,"search")).getText());
   for(String n:new String[]{"x","y","width","height","split","selectedPackage","packageScroll","resourceScroll","selected","rows"})ui.put(n,field(browser,n));
   if(mc.currentScreen instanceof net.minecraft.client.gui.inventory.GuiContainer) {
    net.minecraft.client.gui.inventory.GuiContainer gui=(net.minecraft.client.gui.inventory.GuiContainer)mc.currentScreen;
    java.util.List<Object> slots=new ArrayList<Object>();
    int left=(Integer)field(gui,"guiLeft"),top=(Integer)field(gui,"guiTop");
    for(Object v:gui.inventorySlots.inventorySlots) {net.minecraft.inventory.Slot slot=(net.minecraft.inventory.Slot)v;Map<String,Object> sm=new LinkedHashMap<String,Object>();
     sm.put("slot",slot.slotNumber);sm.put("x",left+slot.xDisplayPosition);sm.put("y",top+slot.yDisplayPosition);sm.put("stack",slot.getStack()==null?"":slot.getStack().toString());slots.add(sm);}
    ui.put("slots",slots);ui.put("window",gui.inventorySlots.windowId);
   }
   out.put("nominator",ui);
  }
  out.put("taskRevision",''')
a=s.rfind('\n}')
s=s[:a]+'''
 static Object field(Object o,String name)throws Exception {Class<?> c=o.getClass();while(c!=null){try{Field f=c.getDeclaredField(name);f.setAccessible(true);return f.get(o);}catch(NoSuchFieldException e){c=c.getSuperclass();}}throw new NoSuchFieldException(name);}
 static void server0323(JsonObject r)throws Exception {
  EntityPlayerMP p=player();World w=p.worldObj;String op=r.get("op").getAsString();
  if(op.equals("fixture0323")) {
   p.setPositionAndUpdate(0,5,0);w.getGameRules().setOrCreateGameRule("doMobSpawning","false");w.setWorldTime(1000);
   hostA=(Entity)Class.forName("noppes.npcs.entity.EntityCustomNpc").getConstructor(World.class).newInstance(w);
   hostB=(Entity)Class.forName("noppes.npcs.entity.EntityCustomNpc").getConstructor(World.class).newInstance(w);
   hostA.setPosition(0,5,3);hostB.setPosition(2,5,3);w.spawnEntityInWorld(hostA);w.spawnEntityInWorld(hostB);
   p.inventory.setInventorySlotContents(0,new ItemStack(ModItems.nominator));
   p.inventory.setInventorySlotContents(1,new ItemStack(net.minecraft.init.Items.stick,16));
   p.inventory.setInventorySlotContents(2,new ItemStack(net.minecraft.init.Items.iron_sword,1,1));
   p.inventory.setInventorySlotContents(3,new ItemStack(net.minecraft.init.Items.iron_sword,1,2));
   p.inventory.setInventorySlotContents(4,new ItemStack(net.minecraft.init.Items.diamond,16));
   p.inventoryContainer.detectAndSendChanges();
  }
  if(op.equals("removeA")) {hostA.setDead();w.removeEntity(hostA);}
  if(op.equals("seedgroups")) {
   String id=r.get("id").getAsString();darkgrey.rpg.nominator.NominatorSavedData d=darkgrey.rpg.nominator.NominatorSavedData.get();
   d.put(new darkgrey.rpg.nominator.NominatorEntityBinding(hostA.getUniqueID(),null,Collections.singletonList(id),null));
   d.put(new darkgrey.rpg.nominator.NominatorEntityBinding(hostB.getUniqueID(),null,Collections.singletonList(id),null));d.addTypeGroup("Pig",id);
  }
  darkgrey.rpg.identity.NpcIdentitySavedData n=darkgrey.rpg.identity.NpcIdentitySavedData.get();
  darkgrey.rpg.nominator.NominatorSavedData d=darkgrey.rpg.nominator.NominatorSavedData.get();
  darkgrey.rpg.item.identity.ItemIdentitySavedData i=darkgrey.rpg.item.identity.ItemIdentitySavedData.get();
  Map<String,Object> a=new LinkedHashMap<String,Object>();a.put("npc",n.bindings());a.put("entities",d.bindings());a.put("typeGroups",d.typeGroups());
  a.put("A",hostA==null?null:hostA.getUniqueID().toString());a.put("B",hostB==null?null:hostB.getUniqueID().toString());a.put("Adead",hostA!=null&&hostA.isDead);
  a.put("revision",i.getRevision());NBTTagCompound tag=new NBTTagCompound();i.writeToNBT(tag);a.put("items",tag.toString());
  a.put("packages",darkgrey.rpg.nominator.NominatorCatalog.from(DarkGreyRpg.getProjectRepository().getSnapshot(),DarkGreyRpg.getStoryPackageLoader().getPackages()).getPackageChoices());
  List<String> inv=new ArrayList<String>();for(ItemStack stack:p.inventory.mainInventory)inv.add(stack==null?"":stack.toString());a.put("inventory",inv);
  audit=a;
 }
'''+s[a:]
(new/'live-src/dgr/acceptance/LiveHarness.java').write_text(s,encoding='utf8')
s=(old/'live.gradle').read_text(encoding='utf8').replace('0322','0323').replace('0.3.2.2','0.3.2.3');(new/'live.gradle').write_text(s,encoding='utf8')
s=(old/'live_driver.py').read_text(encoding='utf8').replace('32261','32361');(new/'live_driver.py').write_text(s,encoding='utf8')
p=R/'src/test/java/darkgrey/rpg/nominator/Nominator0323Probe.java';s=p.read_text(encoding='utf8').replace('String browser = source("client/gui/NominatorBrowser.java");','String browser = source("client/gui/NominatorBrowser.java").replaceAll("\\\\s+", "");').replace('!browser.contains("top + 20")','!browser.contains("top+20")');p.write_text(s,encoding='utf8')
