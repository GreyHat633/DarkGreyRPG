package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.renderer.RenderHelper;
import net.minecraft.client.renderer.entity.RenderItem;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.opengl.GL11;

/** Shared measured item row for task menus, chooser and HUD. No inventory interactions. */
public final class ItemSlotStrip {

    private static final RenderItem RENDERER = new RenderItem();
    private final NBTTagCompound source;
    private NBTTagList items;
    private final boolean collapsed;
    private final List<ItemStack> stacks = new ArrayList<ItemStack>();
    public List<String> tooltip;

    public ItemSlotStrip(NBTTagCompound source, boolean collapsed) {
        this.source = (NBTTagCompound) source.copy();
        this.items = this.source.getTagList("items", 10);
        this.collapsed = collapsed;
        for (int i = 0; i < items.tagCount(); i++) {
            ItemStack stack = null;
            try {
                if (items.getCompoundTagAt(i)
                    .hasKey("stack", 10))
                    stack = ItemStack.loadItemStackFromNBT(
                        items.getCompoundTagAt(i)
                            .getCompoundTag("stack"));
            } catch (RuntimeException ignored) {}
            stacks.add(stack);
        }
    }

    public static ItemSlotStrip rewards(NBTTagList rewards) {
        NBTTagCompound data = new NBTTagCompound();
        data.setTag("items", rewards);
        return new ItemSlotStrip(data, false);
    }

    public NBTTagCompound source() {
        return (NBTTagCompound) source.copy();
    }

    private int count() {
        return collapsed ? Math.min(1, items.tagCount()) : items.tagCount();
    }

    private String experience(NBTTagCompound row) {
        return "经验 " + (row.getInteger("amount") >= 0 ? "+" : "−") + Math.abs((long) row.getInteger("amount"));
    }

    private int cellWidth(int i) {
        NBTTagCompound row = items.getCompoundTagAt(i);
        Minecraft mc = Minecraft.getMinecraft();
        if ("xp".equals(row.getString("type"))) return mc.fontRenderer.getStringWidth(experience(row)) + 8;
        long amount = Math.abs((long) row.getInteger("amount"));
        return 20 + (row.getInteger("amount") < 0 ? 26 : 0)
            + (amount > 99 ? mc.fontRenderer.getStringWidth(Long.toString(amount)) + 4 : 0);
    }

    public int preferredWidth() {
        int result = 0;
        for (int i = 0; i < count(); i++) result += cellWidth(i) + (i == 0 ? 0 : 4);
        return Math.max(20, result);
    }

    public int height(int width) {
        int x = 0, rows = count() == 0 ? 0 : 1;
        for (int i = 0; i < count(); i++) {
            int w = cellWidth(i);
            if (x > 0 && x + w > width) {
                rows++;
                x = 0;
            }
            x += w + 4;
        }
        return rows * 24 + (source.hasKey("notice") ? 14 : 0);
    }

    public void draw(int left, int top, int width, int clipTop, int clipBottom, int mouseX, int mouseY) {
        Minecraft mc = Minecraft.getMinecraft();
        tooltip = null;
        int x = left, y = top;
        if (collapsed && source.getBoolean("group") && top >= clipTop && top + 20 <= clipBottom) {
            if (mouseX >= 0 && mouseY >= 0) ItemCandidatePopover.offer(source, left, top, mouseX, mouseY);
            boolean freeze = false;
            NBTTagCompound candidate = darkgrey.rpg.client.TaskPresentationPages.candidate(source, freeze);
            if (candidate != null && (items.tagCount() != 1 || !items.getCompoundTagAt(0)
                .equals(candidate))) {
                items = new NBTTagList();
                items.appendTag(candidate);
                stacks.clear();
                stacks.add(
                    candidate.hasKey("stack", 10) ? ItemStack.loadItemStackFromNBT(candidate.getCompoundTag("stack"))
                        : null);
            }
        }
        for (int i = 0; i < count(); i++) {
            int w = cellWidth(i);
            if (x > left && x + w > left + width) {
                x = left;
                y += 24;
            }
            if (y >= clipTop && y + 20 <= clipBottom) drawCell(i, x, y, mouseX, mouseY);
            x += w + 4;
        }
        y += count() == 0 ? 0 : 24;
        if (source.hasKey("notice") && y >= clipTop && y + 12 <= clipBottom) {
            mc.fontRenderer.drawString("部分候选未展开 · 悬停查看", left, y, DgrUiPalette.SECONDARY);
            if (mouseX >= left && mouseX < left + width && mouseY >= y && mouseY < y + 12)
                tooltip = java.util.Collections.singletonList(source.getString("notice"));
        }
    }

    private void drawCell(int i, int x, int y, int mouseX, int mouseY) {
        Minecraft mc = Minecraft.getMinecraft();
        NBTTagCompound row = items.getCompoundTagAt(i);
        if ("xp".equals(row.getString("type"))) {
            mc.fontRenderer.drawString(experience(row), x, y + 6, DgrUiPalette.TEXT);
            return;
        }
        int signed = row.getInteger("amount");
        long amount = Math.abs((long) signed);
        if (signed < 0) {
            mc.fontRenderer.drawString("扣除", x, y + 6, DgrUiPalette.SECONDARY);
            x += 26;
        }
        boolean hover = mouseX >= x && mouseX < x + 20 && mouseY >= y && mouseY < y + 20;
        Gui.drawRect(x, y, x + 20, y + 20, DgrUiPalette.BORDER);
        Gui.drawRect(x + 1, y + 1, x + 19, y + 19, hover ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
        ItemStack stack = stacks.get(i);
        if (stack != null) {
            GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
            GL11.glPushMatrix();
            try {
                RenderHelper.enableGUIStandardItemLighting();
                RENDERER.renderItemAndEffectIntoGUI(mc.fontRenderer, mc.getTextureManager(), stack, x + 2, y + 2);
            } finally {
                GL11.glPopMatrix();
                GL11.glPopAttrib();
            }
        } else mc.fontRenderer.drawString("?", x + 7, y + 6, DgrUiPalette.SECONDARY);
        if (amount > 0) {
            String number = Long.toString(amount);
            if (amount <= 99) {
                // Vanilla stack-count pass: unlit white text with shadow, above the item depth.
                // Restoring GL attributes alone does not clear the depth written by a 3D icon.
                GL11.glPushAttrib(GL11.GL_ENABLE_BIT | GL11.GL_COLOR_BUFFER_BIT | GL11.GL_DEPTH_BUFFER_BIT);
                try {
                    GL11.glDisable(GL11.GL_LIGHTING);
                    GL11.glDisable(GL11.GL_DEPTH_TEST);
                    GL11.glDisable(GL11.GL_BLEND);
                    mc.fontRenderer.drawStringWithShadow(
                        number,
                        x + 19 - mc.fontRenderer.getStringWidth(number),
                        y + 11,
                        0xFFFFFF);
                } finally {
                    GL11.glPopAttrib();
                }
            } else mc.fontRenderer.drawString(number, x + 24, y + 6, DgrUiPalette.TEXT);
        }
        if (hover) {
            tooltip = new ArrayList<String>();
            if (stack != null) {
                try {
                    tooltip.addAll(stack.getTooltip(mc.thePlayer, mc.gameSettings.advancedItemTooltips));
                } catch (RuntimeException invalid) {
                    tooltip.add("物品提示不可用");
                }
                if (!tooltip.isEmpty()) tooltip.set(0, stack.getRarity().rarityColor + tooltip.get(0));
            } else tooltip.add(row.hasKey("error") ? row.getString("error") : "物品未绑定或已缺失");

        }
    }

    public boolean moreAt(int x, int y) {
        return false; // Compatibility for older surfaces; candidate interaction belongs to the popover.
    }
}
