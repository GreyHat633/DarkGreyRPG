from pathlib import Path
R=Path.cwd(); base=R/'src/main/java/darkgrey/rpg'
def put(p,s): (base/p).write_text(s,encoding='utf8')
put('network/message/nominator/C2SNominatorAction.java','''package darkgrey.rpg.network.message.nominator;

import java.util.UUID;
import cpw.mods.fml.common.network.simpleimpl.*;
import cpw.mods.fml.common.network.ByteBufUtils;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorActions;
import io.netty.buffer.ByteBuf;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.entity.player.EntityPlayerMP;

/** Versioned, correlated 0.3.2.3 operation; server validates every mutation. */
public final class C2SNominatorAction implements IMessage {
    public NBTTagCompound data;
    public C2SNominatorAction() {}
    public C2SNominatorAction(NBTTagCompound data) { this.data = (NBTTagCompound)data.copy(); }
    public void fromBytes(ByteBuf b) {
        data = ByteBufUtils.readTag(b);
        if (data == null || b.isReadable()) throw new IllegalArgumentException("Invalid action packet");
        UUID.fromString(data.getString("token"));
        if (data.getString("resource").length() > 256 || data.getString("package").length() > 256)
            throw new IllegalArgumentException("Invalid resource");
    }
    public void toBytes(ByteBuf b) { ByteBufUtils.writeTag(b, data); }
    public static final class Handler implements IMessageHandler<C2SNominatorAction,IMessage> {
        public IMessage onMessage(final C2SNominatorAction m, MessageContext ctx) {
            final EntityPlayerMP player = ctx.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {
                public void run() { NominatorActions.handle(player, m.data); }
            });
            return null;
        }
    }
}
''')
put('network/message/nominator/S2CNominatorActionResult.java','''package darkgrey.rpg.network.message.nominator;

import cpw.mods.fml.common.network.simpleimpl.*;
import cpw.mods.fml.common.network.ByteBufUtils;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorCatalog;
import io.netty.buffer.ByteBuf;
import net.minecraft.nbt.NBTTagCompound;

public final class S2CNominatorActionResult implements IMessage {
    public NBTTagCompound data;
    public NominatorCatalog catalog;
    public S2CNominatorActionResult() {}
    public S2CNominatorActionResult(NBTTagCompound data, NominatorCatalog catalog) { this.data=data; this.catalog=catalog; }
    public void fromBytes(ByteBuf b) {
        data=ByteBufUtils.readTag(b); catalog=NominatorCatalogCodec.read(b);
        if (data == null || b.isReadable()) throw new IllegalArgumentException("Invalid result");
    }
    public void toBytes(ByteBuf b) { ByteBufUtils.writeTag(b,data); NominatorCatalogCodec.write(b,catalog); }
    public static final class Handler implements IMessageHandler<S2CNominatorActionResult,IMessage> {
        public IMessage onMessage(final S2CNominatorActionResult m, MessageContext ctx) {
            MainThreadScheduler.scheduleClient(new Runnable() {
                public void run() { DarkGreyRpg.proxy.acceptNominatorResult(m.data,m.catalog); }
            });
            return null;
        }
    }
}
''')
put('nominator/NominatorActions.java','''package darkgrey.rpg.nominator;

import java.util.*;
import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.identity.*;
import darkgrey.rpg.item.identity.*;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.S2CNominatorActionResult;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;
import darkgrey.rpg.project.*;

/** Operations run on the server thread, with revision validation preceding mutation. */
public final class NominatorActions {
    private NominatorActions() {}
    public static void handle(EntityPlayerMP player, NBTTagCompound request) {
        ProjectRepository repo=DarkGreyRpg.getProjectRepository();
        NominatorCatalog catalog=NominatorCatalog.from(repo.getSnapshot(),DarkGreyRpg.getStoryPackageLoader().getPackages());
        NominatorSavedData selections=NominatorSavedData.get();
        NpcIdentitySavedData npc=NpcIdentitySavedData.get();
        ItemIdentitySavedData items=ItemIdentitySavedData.get();
        NominatorResult result;
        try { result=execute(player,request,catalog,repo.getSnapshot(),repo.getSnapshotRevision(),selections,npc,items); }
        catch (RuntimeException e) {
            DarkGreyRpg.LOG.warn("Nominator operation rejected",e);
            result=NominatorResult.rejected("invalid_request","服务器拒绝了该请求。");
        }
        NBTTagCompound response=new NBTTagCompound();
        response.setString("token",request.getString("token"));
        response.setInteger("sequence",request.getInteger("sequence"));
        response.setString("code",result.getCode());
        response.setString("message",result.getExplanation());
        response.setBoolean("accepted",result.isAccepted());
        response.setLong("catalogRevision",repo.getSnapshotRevision());
        response.setLong("revision",request.getBoolean("items")?items.getRevision():selections.getRevision());
        response.setLong("npcRevision",npc.getRevision());
        if (!request.getBoolean("items")) {
            try {
                UUID uuid=UUID.fromString(request.getString("entityUuid"));
                String id=npc.getNpcId(uuid);
                response.setString("individual",id==null?"":id);
                NominatorEntityBinding binding=selections.get(uuid);
                response.setString("groups",binding==null?"":binding.getGroupIds().toString());
            } catch (IllegalArgumentException ignored) {}
        }
        DialogueNetwork.CHANNEL.sendTo(new S2CNominatorActionResult(response,catalog),player);
    }

    private static NominatorResult execute(EntityPlayerMP player,NBTTagCompound q,NominatorCatalog catalog,
        ProjectSnapshot project,long catalogRevision,NominatorSavedData selections,NpcIdentitySavedData npc,ItemIdentitySavedData items) {
        if (!NominatorPermission.canUse(player) || !player.inventory.hasItem(ModItems.nominator))
            return fail("permission_denied","没有使用指名器的权限或背包中没有指名器。");
        String op=q.getString("op"), id=q.getString("resource"), type=q.getString("type");
        boolean item=q.getBoolean("items");
        ContainerNominatorInventory container=null;
        Entity entity=null;
        if (item) {
            if (!(player.openContainer instanceof ContainerNominatorInventory) || player.openContainer.windowId!=q.getInteger("window"))
                return fail("invalid_host","物品容器已关闭。");
            container=(ContainerNominatorInventory)player.openContainer;
            if (!container.canInteractWith(player)) return fail("invalid_host","当前物品容器不可用。");
        } else if (!"release".equals(op)) {
            entity=player.worldObj.getEntityByID(q.getInteger("entity"));
            if (entity==null || !entity.getUniqueID().toString().equals(q.getString("entityUuid"))
                || entity.dimension!=player.dimension || player.getDistanceSqToEntity(entity)>64D)
                return fail("invalid_host","当前实体不可用。");
        }
        if ("sync".equals(op)) return NominatorResult.accepted("");
        if (!q.hasKey("revision",4) || !q.hasKey("catalogRevision",4)
            || q.getLong("catalogRevision")!=catalogRevision
            || q.getLong("revision")!=(item?items.getRevision():selections.getRevision())
            || !item && (!q.hasKey("npcRevision",4) || q.getLong("npcRevision")!=npc.getRevision()))
            return fail("stale_revision","数据已刷新，请重新执行操作。");
        if (!"bind".equals(op) && !"transfer".equals(op) && !"release".equals(op) && !"unbind".equals(op))
            return fail("invalid_request","操作不可用。");
        if (!"unbind".equals(op)) {
            NominatorCatalog.PackageChoice choice=catalog.getPackageChoice(q.getString("package"));
            boolean valid=choice!=null && (item
                ? "Item".equals(type)?choice.containsItem(id):"Item Group".equals(type)&&choice.containsItemGroup(id)
                : ("NPC".equals(type)||"Group".equals(type))&&choice.containsActor(id));
            if (!valid) return fail("invalid_resource","资源不属于所选故事包。");
        }
        if (item) {
            if ("release".equals(op)) return changed("Item".equals(type)?items.unbindItem(id):items.releaseGroup(id));
            int slot="unbind".equals(op)?ContainerNominatorInventory.UNBIND_SLOT:ContainerNominatorInventory.NOMINATE_SLOT;
            ItemStack stack=container.getTargetInventory().getStackInSlot(slot);
            if (stack==null || stack.getItem()==ModItems.nominator) return fail("invalid_host","请先放入一个物品。");
            boolean changed;
            if ("unbind".equals(op)) changed=items.unbindDefinition(stack);
            else if ("Item".equals(type)) {
                ItemStackDefinition definition=ItemStackDefinition.capture(stack), old=items.getItem(id);
                if (old!=null && !old.equals(definition) && !"transfer".equals(op))
                    return fail("transfer_required","ItemID 已被占用，是否转移到指名槽中的物品？");
                changed="transfer".equals(op)?items.transferItem(id,definition):items.bindItem(id,definition);
            } else {
                if ("transfer".equals(op)) return fail("invalid_request","物品组不支持转移。");
                String mode=q.getString("mode");
                if (!"EXACT".equals(mode)&&!"FUZZY".equals(mode)) return fail("invalid_request","请选择匹配方式。");
                changed=items.addGroupMember(id,new ItemGroupMember(ItemMatchMode.valueOf(mode),ItemStackDefinition.capture(stack)));
            }
            container.returnSlotToOwner(player,slot);
            container.detectAndSendChanges();
            return changed(changed);
        }
        if ("release".equals(op)) return NominatorService.releaseEntityResource(true,id,project,npc,selections);
        if ("unbind".equals(op)) return NominatorService.unbindEntity(true,entity.getUniqueID(),NominatorService.entityType(entity),entity.dimension,project,npc,selections);
        ActorDefinition actor=project.getActor(id);
        if (actor==null || "NPC".equals(type)!=actor.isIndividual()) return fail("invalid_resource","角色类型已改变。");
        NominatorEntityBinding old=selections.get(entity.getUniqueID());
        List<String> groups=old==null?new ArrayList<String>():new ArrayList<String>(old.getGroupIds());
        String individual=npc.getNpcId(entity.getUniqueID());
        if (actor.isIndividual()) {
            NpcHostIdentity host=npc.getHost(id);
            if (host!=null && !host.getEntityUuid().equals(entity.getUniqueID()) && !"transfer".equals(op))
                return fail("transfer_required","NPCID 已被占用，是否转移到当前实体？");
            individual=id;
        } else if (!groups.contains(id)) groups.add(id);
        return NominatorService.bindEntity(true,entity.getUniqueID(),NominatorService.entityType(entity),entity.dimension,
            individual,groups,catalog.getPackageChoice(q.getString("package")).getStoryId(),"transfer".equals(op),project,npc,selections);
    }
    private static NominatorResult fail(String code,String message) { return NominatorResult.rejected(code,message); }
    private static NominatorResult changed(boolean changed) {
        return changed?NominatorResult.accepted("操作完成，资源已保留。"):NominatorResult.noop("绑定没有变化。");
    }
}
''')
p=base/'network/NominatorNetwork.java';s=p.read_text();s=s.replace('        registered = true;', '''        DialogueNetwork.CHANNEL.registerMessage(darkgrey.rpg.network.message.nominator.C2SNominatorAction.Handler.class,
            darkgrey.rpg.network.message.nominator.C2SNominatorAction.class, 19, Side.SERVER);
        DialogueNetwork.CHANNEL.registerMessage(darkgrey.rpg.network.message.nominator.S2CNominatorActionResult.Handler.class,
            darkgrey.rpg.network.message.nominator.S2CNominatorActionResult.class, 20, Side.CLIENT);
        registered = true;''');p.write_text(s,encoding='utf8')
p=base/'proxy/CommonProxy.java';s=p.read_text().replace('    public void registerClientDialogueNetwork()', '    public void acceptNominatorResult(net.minecraft.nbt.NBTTagCompound data, NominatorCatalog catalog) {}\n\n    public void registerClientDialogueNetwork()');p.write_text(s,encoding='utf8')
p=base/'client/ClientProxy.java';s=p.read_text().replace('    private NominatorCatalog pendingInventoryCatalog;', '''    @Override
    public void acceptNominatorResult(net.minecraft.nbt.NBTTagCompound data, NominatorCatalog catalog) {
        net.minecraft.client.gui.GuiScreen screen=Minecraft.getMinecraft().currentScreen;
        if (screen instanceof GuiNominatorInventory) ((GuiNominatorInventory)screen).acceptResult(data,catalog);
        if (screen instanceof GuiNominatorEntity) ((GuiNominatorEntity)screen).acceptResult(data,catalog);
    }

    private NominatorCatalog pendingInventoryCatalog;''');p.write_text(s,encoding='utf8')
