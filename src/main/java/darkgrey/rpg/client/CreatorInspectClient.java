package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.renderer.entity.RenderManager;
import net.minecraft.entity.Entity;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.World;
import net.minecraftforge.client.event.RenderWorldLastEvent;
import net.minecraftforge.event.entity.player.ItemTooltipEvent;

import org.lwjgl.opengl.GL11;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;

public final class CreatorInspectClient {

    private static World world;
    private static boolean enabled;
    private static int dimension;
    private static long revision = -1;
    private static final Map<Integer, NBTTagCompound> entities = new LinkedHashMap<Integer, NBTTagCompound>();
    private static ItemIdentitySavedData catalog;
    private static final Map<ItemStackDefinition, List<String>> cache = new LinkedHashMap<ItemStackDefinition, List<String>>();

    public static void accept(NBTTagCompound data) {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null || mc.thePlayer.dimension != data.getInteger("dimension"))
            return;
        if (world != mc.theWorld) clear();
        world = mc.theWorld;
        dimension = data.getInteger("dimension");
        enabled = data.getBoolean("enabled");
        entities.clear();
        if (!enabled) {
            catalog = null;
            cache.clear();
            revision = -1;
            return;
        }
        if (data.hasKey("catalog", 10)) {
            ItemIdentitySavedData incoming = new ItemIdentitySavedData();
            incoming.readFromNBT(data.getCompoundTag("catalog"));
            catalog = incoming;
            cache.clear();
            revision = data.getLong("revision");
        } else if (revision != data.getLong("revision")) {
            catalog = null;
            cache.clear();
        }
        NBTTagList list = data.getTagList("entities", 10);
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound row = list.getCompoundTagAt(i);
            entities.put(row.getInteger("entity"), row);
        }
    }

    private static void clear() {
        world = null;
        enabled = false;
        entities.clear();
        cache.clear();
        catalog = null;
        revision = -1;
    }

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.END && world != Minecraft.getMinecraft().theWorld) clear();
    }

    @SubscribeEvent
    public void tooltip(ItemTooltipEvent event) {
        if (!enabled || catalog == null
            || Minecraft.getMinecraft().theWorld != world
            || Minecraft.getMinecraft().currentScreen == null) return;
        ItemStackDefinition key = ItemStackDefinition.capture(event.itemStack);
        List<String> lines = cache.get(key);
        if (lines == null) {
            lines = new ArrayList<String>();
            List<String> items = catalog.matchingItemIds(event.itemStack),
                groups = catalog.matchingGroupIds(event.itemStack);
            for (String id : items) lines.add("\u00a7e[ItemID] " + id);
            for (String id : groups) lines.add("\u00a7b[GroupID] " + id);
            if (cache.size() >= 256) cache.clear();
            cache.put(key, lines);
        }
        event.toolTip.addAll(lines);
    }

    @SubscribeEvent
    public void render(RenderWorldLastEvent event) {
        Minecraft mc = Minecraft.getMinecraft();
        if (!enabled || world != mc.theWorld || mc.thePlayer == null) return;
        for (Integer entityId : entities.keySet()) {
            Entity entity = world.getEntityByID(entityId);
            if (entity == null || entity.isDead) continue;
            if (!enabled || world != mc.theWorld
                || mc.thePlayer == null
                || entity.dimension != dimension
                || entity.getDistanceSqToEntity(mc.thePlayer) > 1024) continue;
            NBTTagCompound row = entities.get(entity.getEntityId());
            // Minecraft 1.7 does not synchronize living-entity UUIDs to clients.
            // The server supplies transient entity IDs, scoped to this client world/dimension.
            if (row == null) continue;
            String[] lines = row.getString("text")
                .split("\n");
            GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
            GL11.glPushMatrix();
            try {
                GL11.glTranslated(
                    entity.lastTickPosX + (entity.posX - entity.lastTickPosX) * event.partialTicks
                        - RenderManager.instance.viewerPosX,
                    entity.lastTickPosY + (entity.posY - entity.lastTickPosY) * event.partialTicks
                        - RenderManager.instance.viewerPosY
                        + entity.height
                        + 0.6
                        + lines.length * 0.25,
                    entity.lastTickPosZ + (entity.posZ - entity.lastTickPosZ) * event.partialTicks
                        - RenderManager.instance.viewerPosZ);
                GL11.glRotatef(-RenderManager.instance.playerViewY, 0, 1, 0);
                GL11.glRotatef(RenderManager.instance.playerViewX, 1, 0, 0);
                GL11.glScalef(-0.025F, -0.025F, 0.025F);
                GL11.glDisable(GL11.GL_LIGHTING);
                GL11.glEnable(GL11.GL_BLEND);
                GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
                GL11.glDepthMask(false);
                for (int i = 0; i < lines.length; i++) {
                    int half = mc.fontRenderer.getStringWidth(lines[i]) / 2;
                    Gui.drawRect(-half - 2, i * 10 - 1, half + 2, i * 10 + 9, 0xA0000000);
                    mc.fontRenderer.drawString(lines[i], -half, i * 10, 0xEEDDCC);
                }
            } finally {
                GL11.glPopMatrix();
                GL11.glPopAttrib();
            }
        }
    }
}
