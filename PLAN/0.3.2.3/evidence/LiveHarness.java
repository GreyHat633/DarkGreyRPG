package dgr.acceptance;
import com.google.gson.*;
import com.sun.net.httpserver.*;
import java.net.*;
import java.io.*;
import java.util.*;
import java.util.concurrent.*;
import java.lang.reflect.*;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.event.FMLInitializationEvent;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.*;
import net.minecraft.world.*;
import net.minecraft.entity.*;
import net.minecraft.entity.player.*;
import net.minecraft.item.*;
import net.minecraft.nbt.*;
import darkgrey.rpg.*;
import darkgrey.rpg.network.*;
import darkgrey.rpg.content.*;

@Mod(modid="dgr0323acceptance",name="DGR acceptance harness",version="1")
public class LiveHarness {
 static volatile Map<String,Object> audit=new LinkedHashMap<String,Object>();
 static Entity hostA,hostB;
 static final Gson GSON = new Gson();
 @Mod.EventHandler public void init(FMLInitializationEvent event) throws Exception {
  HttpServer api = HttpServer.create(new InetSocketAddress("127.0.0.1",32361),0);
  api.createContext("/", new HttpHandler(){ public void handle(HttpExchange exchange) throws IOException {
   final JsonObject request;
   try { request = new JsonParser().parse(new InputStreamReader(exchange.getRequestBody(),"UTF-8")).getAsJsonObject(); }
   catch(Exception ex) {exchange.sendResponseHeaders(400,-1);exchange.close();return;}
   final CompletableFuture<String> result = new CompletableFuture<String>();
   MainThreadScheduler.scheduleClient(new Runnable(){public void run(){try{result.complete(GSON.toJson(action(request)));}catch(Throwable ex){result.complete(GSON.toJson(Collections.singletonMap("error",ex.toString())));ex.printStackTrace();}}});
   byte[] response;try{response=result.get(45,TimeUnit.SECONDS).getBytes("UTF-8");}catch(Exception ex){response=ex.toString().getBytes("UTF-8");}
   exchange.getResponseHeaders().add("Content-Type","application/json; charset=utf-8"); exchange.sendResponseHeaders(200,response.length);
   exchange.getResponseBody().write(response);exchange.close();
  }});api.start();
 }
 static Object action(final JsonObject r) throws Exception {
  final Minecraft mc=Minecraft.getMinecraft();String op=r.get("op").getAsString();
  if(op.equals("stale0323")) {
   Object controls=field(mc.currentScreen,"controls");Field f=controls.getClass().getDeclaredField("revision");f.setAccessible(true);f.setLong(controls,f.getLong(controls)-1);
  }
  if(op.equals("invalidresource0323")) {
   darkgrey.rpg.client.gui.NominatorControls controls=(darkgrey.rpg.client.gui.NominatorControls)field(mc.currentScreen,"controls");
   NBTTagCompound q=new NBTTagCompound();q.setBoolean("items",true);q.setInteger("window",((net.minecraft.client.gui.inventory.GuiContainer)mc.currentScreen).inventorySlots.windowId);q.setString("op","release");q.setString("package","Consumer:story");q.setString("resource","GreyHat_:CopperCoin");q.setString("type","Item");controls.begin(q,"");
  }
  if(op.equals("audit0323")||op.equals("fixture0323")||op.equals("removeA")||op.equals("seedgroups")||op.equals("items0323")) {
   MainThreadScheduler.scheduleServer(new Runnable(){public void run(){try{server0323(r);}catch(Exception e){audit=Collections.<String,Object>singletonMap("error",e.toString());e.printStackTrace();}}});
  }
  if(op.equals("openhost")) {
   Entity e=r.get("host").getAsString().equals("A")?hostA:hostB;
   DialogueNetwork.CHANNEL.sendToServer(new darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen(e.getEntityId()));
  }
  if(op.equals("start")) mc.launchIntegratedServer("DGR0323Final","DGR 0.3.2.3 Acceptance",new WorldSettings(321L,WorldSettings.GameType.CREATIVE,true,false,WorldType.FLAT).enableCommands());
  if(op.equals("command")) mc.thePlayer.sendChatMessage(r.get("text").getAsString());
  if(op.equals("narration")) {
   final darkgrey.rpg.network.message.canonical.CanonicalSessionFrame f=darkgrey.rpg.client.session.CanonicalSessionClientController.getFrame();
   if(f==null || !f.canContinue())throw new IllegalStateException("Active line required");
   MainThreadScheduler.scheduleServer(new Runnable(){public void run(){DialogueNetwork.CHANNEL.sendTo(new darkgrey.rpg.network.message.canonical.CanonicalSessionFrame(f.getTransportId(),f.getStoryId(),f.getSessionResourceId(),f.getCurrentNodeId(),darkgrey.rpg.network.message.canonical.CanonicalSessionFrame.Kind.NARRATION,"",f.getText(),f.getChoices()),player());}});
  }
  if(op.equals("click")) {
   Method m=GuiScreen.class.getDeclaredMethod("mouseClicked",int.class,int.class,int.class);m.setAccessible(true);
   GuiScreen screen=mc.currentScreen;
   m.invoke(screen,r.get("x").getAsInt(),r.get("y").getAsInt(),r.has("button")?r.get("button").getAsInt():0);
   Method up=GuiScreen.class.getDeclaredMethod("mouseMovedOrUp",int.class,int.class,int.class);up.setAccessible(true);
   up.invoke(screen,r.get("x").getAsInt(),r.get("y").getAsInt(),r.has("button")?r.get("button").getAsInt():0);
  }
  if(op.equals("key")) {
   Method m=GuiScreen.class.getDeclaredMethod("keyTyped",char.class,int.class);m.setAccessible(true);
   String text=r.has("text")?r.get("text").getAsString():"";
   if(text.isEmpty())m.invoke(mc.currentScreen,'\0',r.get("key").getAsInt());
   else for(char c:text.toCharArray())m.invoke(mc.currentScreen,c,0);
  }
  if(op.equals("wheel")) {
   for(String name:new String[]{"event_dwheel","event_x","event_y","eventButton"}) {
    Field f=org.lwjgl.input.Mouse.class.getDeclaredField(name);f.setAccessible(true);
    f.setInt(null,name.equals("event_dwheel")?r.get("delta").getAsInt():name.equals("event_x")?r.get("x").getAsInt()*mc.displayWidth/mc.currentScreen.width:name.equals("event_y")?mc.displayHeight-r.get("y").getAsInt()*mc.displayHeight/mc.currentScreen.height-1:-1);
   }
   mc.currentScreen.handleMouseInput();
  }
  if(op.equals("mousefields")) return Arrays.toString(org.lwjgl.input.Mouse.class.getDeclaredFields());
  if(op.equals("probe")) darkgrey.rpg.creator.CreatorUxProbe.main(new String[]{"live"});
  if(op.equals("hotbar")) {mc.thePlayer.inventory.currentItem=r.get("slot").getAsInt();mc.thePlayer.sendQueue.addToSendQueue(new net.minecraft.network.play.client.C09PacketHeldItemChange(r.get("slot").getAsInt()));}
  if(op.equals("resize")) {org.lwjgl.opengl.Display.setDisplayMode(new org.lwjgl.opengl.DisplayMode(r.get("width").getAsInt(),r.get("height").getAsInt()));mc.resize(r.get("width").getAsInt(),r.get("height").getAsInt());}
  if(op.equals("hover")) org.lwjgl.input.Mouse.setCursorPosition(r.get("x").getAsInt(),mc.displayHeight-r.get("y").getAsInt());
  if(op.equals("remapchat")) {mc.gameSettings.keyBindChat.setKeyCode(r.get("key").getAsInt());net.minecraft.client.settings.KeyBinding.resetKeyBindingArrayAndHash();}
  if(op.equals("chat")) mc.displayGuiScreen(new GuiChat());
  if(op.equals("testgui")) mc.displayGuiScreen(new GuiScreen(){public void drawScreen(int x,int y,float t){drawDefaultBackground();drawCenteredString(fontRendererObj,"Higher-priority test GUI",width/2,30,0xFFFFFF);}public boolean doesGuiPauseGame(){return false;}});
  if(op.equals("chest")) mc.displayGuiScreen(new net.minecraft.client.gui.inventory.GuiChest(mc.thePlayer.inventory,new net.minecraft.inventory.InventoryBasic("Acceptance chest",true,27)));
  if(op.equals("copier")) mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiCopierTemplates(mc.thePlayer.getHeldItem()));
  if(op.equals("hotkey")) net.minecraft.client.settings.KeyBinding.onTick(r.get("key").getAsInt());
  if(op.equals("tasks")) mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiCanonicalTaskScreen());
  if(op.equals("inventory")) mc.displayGuiScreen(new net.minecraft.client.gui.inventory.GuiInventory(mc.thePlayer));
  if(op.equals("close")) mc.displayGuiScreen(null);
  if(op.equals("scale")) {mc.gameSettings.guiScale=r.get("value").getAsInt();mc.resize(mc.displayWidth,mc.displayHeight);}
  if(op.equals("doubleclick")) {
   GuiScreen screen=mc.currentScreen;Method m=GuiScreen.class.getDeclaredMethod("mouseClicked",int.class,int.class,int.class);m.setAccessible(true);
   m.invoke(screen,r.get("x").getAsInt(),r.get("y").getAsInt(),0);m.invoke(screen,r.get("x").getAsInt(),r.get("y").getAsInt(),0);
  }
  if(op.equals("shot")) return net.minecraft.util.ScreenShotHelper.saveScreenshot(mc.mcDataDir,r.get("name").getAsString()+".png",mc.displayWidth,mc.displayHeight,mc.getFramebuffer()).getUnformattedText();
  if(op.equals("quit")) {mc.theWorld.sendQuittingDisconnectingPacket();mc.loadWorld(null);mc.displayGuiScreen(new GuiMainMenu());}
  if(op.equals("shutdown")) mc.shutdown();
  if(op.equals("entitygui")) {
   for(Object v:mc.theWorld.loadedEntityList) if(r.has("type") ? r.get("type").getAsString().equals(EntityList.getEntityString((Entity)v)) : v.getClass().getName().equals("noppes.npcs.entity.EntityCustomNpc")) {
    Entity e=(Entity)v;if(e.getDistanceSqToEntity(mc.thePlayer)>64)continue;DialogueNetwork.CHANNEL.sendToServer(new darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen(e.getEntityId()));break;
   }
  }
  if(op.equals("itemgui")) DialogueNetwork.CHANNEL.sendToServer(new darkgrey.rpg.network.message.nominator.C2SNominatorInventoryOpen());
  if(op.equals("setup") || op.equals("goggles") || op.equals("kill") || op.equals("interact") || op.equals("slime") || op.equals("serverstate") || op.equals("die") || op.equals("copiersetup")) {
   MainThreadScheduler.scheduleServer(new Runnable(){public void run(){try{server(r);}catch(Exception ex){ex.printStackTrace();}}});
  }
  Map<String,Object> out=new LinkedHashMap<String,Object>();out.put("screen",mc.currentScreen==null?"world":mc.currentScreen.getClass().getName());
  if(mc.currentScreen!=null){out.put("width",mc.currentScreen.width);out.put("height",mc.currentScreen.height);}
  if(mc.thePlayer!=null){out.put("player",mc.thePlayer.getUniqueID().toString());out.put("position",Arrays.asList(mc.thePlayer.posX,mc.thePlayer.posY,mc.thePlayer.posZ));}
  Class<?> inspect=darkgrey.rpg.client.CreatorInspectClient.class;
  for(String field:new String[]{"enabled","revision","entities"}) {Field f=inspect.getDeclaredField(field);f.setAccessible(true);out.put("inspect_"+field,String.valueOf(f.get(null)));}
  if(mc.thePlayer!=null && mc.thePlayer.getHeldItem()!=null && mc.thePlayer.getHeldItem().getItem()==ModItems.copier){out.put("copierState",darkgrey.rpg.item.ItemCopier.loadState(mc.thePlayer.getHeldItem()));}
    if(mc.thePlayer!=null){out.put("heldSlot",mc.thePlayer.inventory.currentItem);out.put("tooltip",mc.thePlayer.inventory.getStackInSlot(2)==null?"empty":mc.thePlayer.inventory.getStackInSlot(2).getTooltip(mc.thePlayer,true));out.put("heldTooltip",mc.thePlayer.getHeldItem()==null?"empty":mc.thePlayer.getHeldItem().getTooltip(mc.thePlayer,true));}
  out.put("audit",audit);
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
  out.put("taskRevision",darkgrey.rpg.client.CanonicalTaskClientStore.getRevision());
  out.put("tasks",String.valueOf(darkgrey.rpg.client.CanonicalTaskClientStore.getSnapshot()));
  out.put("modelFrame",darkgrey.rpg.client.session.CanonicalSessionClientController.getFrame());
  if(mc.currentScreen != null) {Field f=GuiScreen.class.getDeclaredField("buttonList");f.setAccessible(true);out.put("buttons",f.get(mc.currentScreen));}
  if(mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiCanonicalSessionScreen) {
   Field f=mc.currentScreen.getClass().getDeclaredField("frame");f.setAccessible(true);
   out.put("frame",f.get(mc.currentScreen));out.put("visibleText",darkgrey.rpg.client.session.CanonicalSessionClientController.getVisibleText());
  }
  return out;
 }
 static EntityPlayerMP player(){return (EntityPlayerMP)net.minecraft.server.MinecraftServer.getServer().getConfigurationManager().playerEntityList.get(0);}
 static void server(JsonObject r) throws Exception {
  EntityPlayerMP p=player();World w=p.worldObj;String op=r.get("op").getAsString();
  if(op.equals("copiersetup")) {
   darkgrey.rpg.entitytools.CopierState state=new darkgrey.rpg.entitytools.CopierState();
   for(int i=0;i<18;i++) {NBTTagCompound config=new NBTTagCompound();config.setString("CustomName","Template "+i);
    state.capture(new darkgrey.rpg.entitytools.EntityCapture(UUID.randomUUID(),"Pig",0,true,false,config,new NBTTagCompound(),new NBTTagCompound(),new NBTTagCompound(),new NBTTagCompound(),new NBTTagCompound(),null,Collections.<String>emptyList()));}
   ItemStack stack=new ItemStack(ModItems.copier);darkgrey.rpg.item.ItemCopier.saveState(stack,state);p.inventory.setInventorySlotContents(p.inventory.currentItem,stack);p.inventoryContainer.detectAndSendChanges();
  }
  if(op.equals("die")) p.attackEntityFrom(net.minecraft.util.DamageSource.outOfWorld,10000F);
  if(op.equals("setup")) {
   Field cheats=net.minecraft.world.storage.WorldInfo.class.getDeclaredField("allowCommands");cheats.setAccessible(true);cheats.setBoolean(w.getWorldInfo(),true);
   p.setPositionAndUpdate(0,5,0);p.rotationYaw=0;p.rotationPitch=0;
   w.getGameRules().setOrCreateGameRule("doMobSpawning","false");w.setWorldTime(1000);
   Entity npc=(Entity)Class.forName("noppes.npcs.entity.EntityCustomNpc").getConstructor(World.class).newInstance(w);
   npc.setPosition(0,5,3);w.spawnEntityInWorld(npc);
   p.inventory.addItemStackToInventory(new ItemStack(ModItems.nominator));p.inventory.addItemStackToInventory(new ItemStack(ModItems.inspectorGoggles));
   p.inventory.addItemStackToInventory(new ItemStack(net.minecraft.init.Items.stick,16));
   System.out.println("LIVE_SETUP_NPC="+npc.getEntityId());
  }
  if(op.equals("goggles")){p.inventory.armorInventory[3]=r.get("on").getAsBoolean()?new ItemStack(ModItems.inspectorGoggles):null;p.inventoryContainer.detectAndSendChanges();}
  Entity npc=null;for(Object v:w.loadedEntityList)if(v.getClass().getName().equals("noppes.npcs.entity.EntityCustomNpc")){npc=(Entity)v;break;}
  if(op.equals("interact")) {
   if(r.has("actor")) {npc=null;for(Object v:w.loadedEntityList)if(darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds((Entity)v).contains(r.get("actor").getAsString())){npc=(Entity)v;break;}}
   if(npc==null)throw new IllegalStateException("No bound actor"); p.interactWith(npc);
  }
  if(op.equals("slime")) {Entity slime=EntityList.createEntityByName("Slime",w);slime.setPosition(2,5,3);w.spawnEntityInWorld(slime);
   if(r.has("actor")) {
    darkgrey.rpg.nominator.NominatorResult result=darkgrey.rpg.nominator.NominatorService.bindEntity(true,slime.getUniqueID(),"Slime",p.dimension,null,Collections.singletonList(r.get("actor").getAsString()),null,true,DarkGreyRpg.getProjectRepository().getSnapshot(),darkgrey.rpg.identity.NpcIdentitySavedData.get(),darkgrey.rpg.nominator.NominatorSavedData.get());
    if(!result.isAccepted())throw new IllegalStateException(result.getCode());
   }
  }
  if(op.equals("kill")) {
   Entity slime=null;for(Object v:w.loadedEntityList)if(v instanceof EntityLivingBase && !((Entity)v).isDead && ((EntityLivingBase)v).getHealth()>0 && darkgrey.rpg.identity.EntityDgrIdentityResolver.resolveActorIds((Entity)v).contains(r.has("actor")?r.get("actor").getAsString():"slimes")){slime=(Entity)v;break;}
   if(slime==null) throw new IllegalStateException("No bound slime available");
   System.out.println("LIVE_ATTACK="+slime.getEntityId()+" health="+((EntityLivingBase)slime).getHealth());
   ((EntityLivingBase)slime).attackEntityFrom(net.minecraft.util.DamageSource.causePlayerDamage(p),10000F);
  }
  if(op.equals("serverstate")) {
   NBTTagCompound state=new NBTTagCompound();darkgrey.rpg.creator.CreatorInspectSavedData.get().writeToNBT(state);
   state.setTag("tasks",darkgrey.rpg.creator.CanonicalTaskUiProjection.project(DarkGreyRpg.getCanonicalTaskManager().getJournal(p)));
   net.minecraft.nbt.CompressedStreamTools.writeCompressed(state,new FileOutputStream(new File(Minecraft.getMinecraft().mcDataDir,"serverstate.nbt")));
   System.out.println("LIVE_STATE="+state);
   System.out.println("LIVE_TASK_RUNTIME="+GSON.toJson(darkgrey.rpg.task.persistence.CanonicalTaskSavedData.get(p).snapshots()));
   System.out.println("LIVE_SESSION_RUNTIME="+GSON.toJson(darkgrey.rpg.session.persistence.CanonicalSessionSavedData.get(p).snapshots()));
   for(String id:new String[]{"Provider:story","Consumer:story","kill_slimes"}) {
    darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot snap=DarkGreyRpg.getCanonicalStoryManager().snapshot(p,id);
    System.out.println("LIVE_STORY="+id+" "+(snap==null?"absent":snap.getActivationTime()+" "+snap.getStatus()));
   }
  }
 }
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
  if(op.equals("removeA")) {hostA.isDead=true;w.removePlayerEntityDangerously(hostA);hostA.isDead=true;}
  if(op.equals("seedgroups")) {
   String id=r.get("id").getAsString();darkgrey.rpg.nominator.NominatorSavedData d=darkgrey.rpg.nominator.NominatorSavedData.get();
   d.put(new darkgrey.rpg.nominator.NominatorEntityBinding(hostA.getUniqueID(),null,Collections.singletonList(id),null));
   d.put(new darkgrey.rpg.nominator.NominatorEntityBinding(hostB.getUniqueID(),null,Collections.singletonList(id),null));d.addTypeGroup("Pig",id);
  }
  darkgrey.rpg.identity.NpcIdentitySavedData n=darkgrey.rpg.identity.NpcIdentitySavedData.get();
  darkgrey.rpg.nominator.NominatorSavedData d=darkgrey.rpg.nominator.NominatorSavedData.get();
  darkgrey.rpg.item.identity.ItemIdentitySavedData i=darkgrey.rpg.item.identity.ItemIdentitySavedData.get();
  Map<String,Object> a=new LinkedHashMap<String,Object>();a.put("npc",n.bindings());a.put("entities",d.bindings());a.put("typeGroups",d.typeGroups());
  a.put("A",hostA==null?null:hostA.getUniqueID().toString());a.put("B",hostB==null?null:hostB.getUniqueID().toString());a.put("Adead",hostA!=null&&hostA.isDead);a.put("Aloaded",hostA!=null&&w.loadedEntityList.contains(hostA));
  Map<String,Object> matches=new LinkedHashMap<String,Object>();
  for(int damage=1;damage<=2;damage++) {ItemStack stack=new ItemStack(net.minecraft.init.Items.iron_sword,1,damage);matches.put("sword"+damage+"Items",i.matchingItemIds(stack));matches.put("sword"+damage+"Groups",i.matchingGroupIds(stack));}
  ItemStack stick=new ItemStack(net.minecraft.init.Items.stick);matches.put("stickItems",i.matchingItemIds(stick));matches.put("stickGroups",i.matchingGroupIds(stick));a.put("matches",matches);
  a.put("revision",i.getRevision());NBTTagCompound tag=new NBTTagCompound();i.writeToNBT(tag);a.put("items",tag.toString());
  a.put("packages",darkgrey.rpg.nominator.NominatorCatalog.from(DarkGreyRpg.getProjectRepository().getSnapshot(),DarkGreyRpg.getStoryPackageLoader().getPackages()).getPackageChoices());
  List<String> inv=new ArrayList<String>();for(ItemStack stack:p.inventory.mainInventory)inv.add(stack==null?"":stack.toString());a.put("inventory",inv);
  audit=a;
 }

}

