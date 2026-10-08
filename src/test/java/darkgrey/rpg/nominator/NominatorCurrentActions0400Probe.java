package darkgrey.rpg.nominator;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.Collections;

import net.minecraft.entity.Entity;
import net.minecraft.entity.monster.EntityZombie;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.entity.player.InventoryPlayer;
import net.minecraft.entity.player.PlayerCapabilities;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.World;
import net.minecraft.world.WorldProvider;
import net.minecraft.world.WorldSettings;
import net.minecraft.world.chunk.IChunkProvider;

import com.mojang.authlib.GameProfile;

import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;
import darkgrey.rpg.project.ProjectSnapshot;

/** Current operation handler fences and physical slot accounting, without retired mutators. */
public final class NominatorCurrentActions0400Probe {

    private static final String ITEM = Nominator0400Probe.STORY + "~item~token";
    private static final String GROUP = Nominator0400Probe.STORY + "~item_group~tokens";
    private static final ProjectSnapshot PROJECT = Nominator0400Probe.project();
    private static final NominatorCatalog CATALOG = new NominatorCatalog(
        Collections.emptyList(),
        Collections.emptyList(),
        Collections.emptyList(),
        Collections.emptyList(),
        Collections.singletonList(
            new NominatorCatalog.PackageChoice(
                "fixture",
                Nominator0400Probe.STORY,
                "Fixture",
                java.util.Arrays.asList(Nominator0400Probe.HERO, Nominator0400Probe.BANDITS),
                Collections.singletonList(ITEM),
                Collections.singletonList(GROUP))));

    private NominatorCurrentActions0400Probe() {}

    public static void main(String[] args) throws Exception {
        Item prior = ModItems.nominator;
        try {
            ModItems.nominator = new Item();
            ProbePlayer player = Nominator0400Probe.allocate(ProbePlayer.class);
            player.capabilities = new PlayerCapabilities();
            player.inventory = new InventoryPlayer(player);
            player.inventory.mainInventory[0] = new ItemStack(ModItems.nominator);
            player.allowed = true;
            ProbeWorld world = Nominator0400Probe.allocate(ProbeWorld.class);
            world.target = Nominator0400Probe.entity(EntityZombie.class, Nominator0400Probe.A);
            world.target.setEntityId(7);
            player.worldObj = world;
            NominatorSavedData selections = new NominatorSavedData();
            NpcIdentitySavedData npc = new NpcIdentitySavedData();
            ItemIdentitySavedData items = new ItemIdentitySavedData();
            NBTTagCompound q = entityRequest(selections, npc);
            player.allowed = false;
            code(player, q, selections, npc, items, "permission_denied");
            player.allowed = true;
            player.inventory.mainInventory[0] = null;
            code(player, q, selections, npc, items, "permission_denied");
            player.inventory.mainInventory[0] = new ItemStack(ModItems.nominator);
            q.setInteger("entity", 8);
            code(player, q, selections, npc, items, "invalid_host");
            q.setInteger("entity", 7);
            q.setString("entityUuid", Nominator0400Probe.B.toString());
            code(player, q, selections, npc, items, "invalid_host");
            q.setString("entityUuid", Nominator0400Probe.A.toString());
            world.target.dimension = 1;
            code(player, q, selections, npc, items, "invalid_host");
            world.target.dimension = 0;
            world.target.posX = 9;
            code(player, q, selections, npc, items, "invalid_host");
            world.target.posX = 0;
            q.setLong("catalogRevision", 6);
            code(player, q, selections, npc, items, "stale_revision");
            q.setLong("catalogRevision", 7);
            q.setLong("revision", 1);
            code(player, q, selections, npc, items, "stale_revision");
            q.setLong("revision", 0);
            q.setLong("npcRevision", 1);
            code(player, q, selections, npc, items, "stale_revision");
            q.setLong("npcRevision", 0);
            q.setString("package", "absent");
            code(player, q, selections, npc, items, "invalid_resource");
            q.setString("package", "fixture");
            q.setString("resource", Nominator0400Probe.STORY + "~actor~unowned");
            code(player, q, selections, npc, items, "invalid_resource");
            q.setString("resource", Nominator0400Probe.BANDITS);
            require(selections.getRevision() == 0 && npc.getRevision() == 0, "all rejected requests were read-only");
            require(execute(player, q, selections, npc, items).isAccepted(), "current group bind");
            require(
                selections.get(Nominator0400Probe.A)
                    .getGroupIds()
                    .contains(Nominator0400Probe.BANDITS),
                "group changed");
            q = entityRequest(selections, npc);
            q.setString("resource", Nominator0400Probe.HERO);
            q.setString("type", "NPC");
            require(execute(player, q, selections, npc, items).isAccepted(), "current individual bind");
            require(Nominator0400Probe.HERO.equals(npc.getNpcId(Nominator0400Probe.A)), "individual changed");
            world.target = Nominator0400Probe.entity(EntityZombie.class, Nominator0400Probe.B);
            world.target.setEntityId(7);
            q = entityRequest(selections, npc);
            q.setString("resource", Nominator0400Probe.HERO);
            q.setString("type", "NPC");
            q.setString("entityUuid", Nominator0400Probe.B.toString());
            long revision = selections.getRevision();
            code(player, q, selections, npc, items, "transfer_required");
            require(selections.getRevision() == revision, "conflict was read-only");
            q.setString("op", "transfer");
            require(execute(player, q, selections, npc, items).isAccepted(), "current explicit transfer");
            require(
                npc.getNpcId(Nominator0400Probe.A) == null
                    && Nominator0400Probe.HERO.equals(npc.getNpcId(Nominator0400Probe.B))
                    && selections.get(Nominator0400Probe.A)
                        .getGroupIds()
                        .contains(Nominator0400Probe.BANDITS),
                "transfer kept old groups");
            q = entityRequest(selections, npc);
            q.setString("entityUuid", Nominator0400Probe.B.toString());
            q.setString("op", "unbind");
            require(
                execute(player, q, selections, npc, items).isAccepted() && selections.get(Nominator0400Probe.B) == null,
                "current entity unbind");
            q = entityRequest(selections, npc);
            q.setString("op", "release");
            world.target = null;
            require(
                execute(player, q, selections, npc, items).isAccepted() && selections.get(Nominator0400Probe.A) == null,
                "current resource release needs no physical host");

            // Item requests must work even when the separate entity data was not read successfully.
            NominatorSavedData quarantined = new NominatorSavedData("failed_entity_reader");
            Item ordinary = new Item();
            Method register = Item.itemRegistry.getClass()
                .getDeclaredMethod("addObjectRaw", int.class, String.class, Object.class);
            register.setAccessible(true);
            register.invoke(Item.itemRegistry, 31040, "probe:current_token", ordinary);
            ContainerNominatorInventory container = new ContainerNominatorInventory(player);
            container.windowId = 4;
            player.openContainer = container;
            ItemStack target = new ItemStack(ordinary, 1, 2);
            container.getTargetInventory()
                .setInventorySlotContents(0, target);
            NBTTagCompound iq = itemRequest(items);
            iq.setInteger("window", 5);
            code(player, iq, quarantined, null, items, "invalid_host");
            iq.setInteger("window", 4);
            iq.setLong("revision", 1);
            code(player, iq, quarantined, null, items, "stale_revision");
            iq.setLong("revision", 0);
            iq.setString("package", "absent");
            code(player, iq, quarantined, null, items, "invalid_resource");
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) == target && items.getRevision() == 0,
                "rejected requests preserved physical slot and bindings");
            iq.setString("package", "fixture");
            require(execute(player, iq, quarantined, null, items).isAccepted(), "item bind independent of entity data");
            require(
                items.matchesItem(ITEM, new ItemStack(ordinary, 1, 2)) && container.getTargetInventory()
                    .getStackInSlot(0) == null && count(player, ordinary) == 1,
                "successful nomination returned physical item");
            container.getTargetInventory()
                .setInventorySlotContents(0, new ItemStack(ordinary, 1, 3));
            iq = itemRequest(items);
            code(player, iq, quarantined, null, items, "transfer_required");
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) != null,
                "declined transfer kept target");
            iq.setString("op", "transfer");
            require(
                execute(player, iq, quarantined, null, items).isAccepted()
                    && items.matchesItem(ITEM, new ItemStack(ordinary, 1, 3))
                    && count(player, ordinary) == 2,
                "item transfer and return");
            container.getTargetInventory()
                .setInventorySlotContents(0, new ItemStack(ordinary, 1, 5));
            iq = itemRequest(items);
            iq.setString("resource", GROUP);
            iq.setString("type", "Item Group");
            iq.setString("mode", "FUZZY");
            require(
                execute(player, iq, quarantined, null, items).isAccepted()
                    && items.matchesGroup(GROUP, new ItemStack(ordinary, 1, 99)),
                "current FUZZY group");
            container.getTargetInventory()
                .setInventorySlotContents(1, new ItemStack(ordinary, 1, 3));
            iq = itemRequest(items);
            iq.setString("op", "unbind");
            require(
                execute(player, iq, quarantined, null, items).isAccepted() && items.getItem(ITEM) == null
                    && items.getGroup(GROUP)
                        .isEmpty()
                    && container.getTargetInventory()
                        .getStackInSlot(1) == null
                    && count(player, ordinary) == 4,
                "current unbind uses second slot and returns it");
            System.out.println("NOMINATOR_0400_CURRENT_ACTION_FENCES=PASS");
            System.out.println("NOMINATOR_0400_CURRENT_ENTITY_BIND_TRANSFER_UNBIND_RELEASE=PASS");
            System.out.println("NOMINATOR_0400_ITEM_QUARANTINE_INDEPENDENCE_SLOT_RETURN=PASS");
        } finally {
            ModItems.nominator = prior;
        }
    }

    private static NBTTagCompound entityRequest(NominatorSavedData selections, NpcIdentitySavedData npc) {
        NBTTagCompound q = base();
        q.setInteger("entity", 7);
        q.setString("entityUuid", Nominator0400Probe.A.toString());
        q.setString("type", "Group");
        q.setString("resource", Nominator0400Probe.BANDITS);
        q.setLong("revision", selections.getRevision());
        q.setLong("npcRevision", npc.getRevision());
        return q;
    }

    private static NBTTagCompound itemRequest(ItemIdentitySavedData items) {
        NBTTagCompound q = base();
        q.setBoolean("items", true);
        q.setInteger("window", 4);
        q.setString("type", "Item");
        q.setString("resource", ITEM);
        q.setLong("revision", items.getRevision());
        return q;
    }

    private static NBTTagCompound base() {
        NBTTagCompound q = new NBTTagCompound();
        q.setString("op", "bind");
        q.setString("package", "fixture");
        q.setLong("catalogRevision", 7);
        return q;
    }

    private static NominatorResult execute(ProbePlayer player, NBTTagCompound q, NominatorSavedData selections,
        NpcIdentitySavedData npc, ItemIdentitySavedData items) throws Exception {
        Method method = NominatorActions.class.getDeclaredMethod(
            "execute",
            EntityPlayerMP.class,
            NBTTagCompound.class,
            NominatorCatalog.class,
            ProjectSnapshot.class,
            long.class,
            NominatorSavedData.class,
            NpcIdentitySavedData.class,
            ItemIdentitySavedData.class);
        method.setAccessible(true);
        try {
            return (NominatorResult) method.invoke(null, player, q, CATALOG, PROJECT, 7L, selections, npc, items);
        } catch (InvocationTargetException e) {
            throw (Exception) e.getCause();
        }
    }

    private static void code(ProbePlayer player, NBTTagCompound q, NominatorSavedData selections,
        NpcIdentitySavedData npc, ItemIdentitySavedData items, String expected) throws Exception {
        NominatorResult result = execute(player, q, selections, npc, items);
        require(!result.isAccepted() && expected.equals(result.getCode()), expected + " was " + result.getCode());
    }

    private static int count(ProbePlayer player, Item item) {
        int count = 0;
        for (ItemStack stack : player.inventory.mainInventory)
            if (stack != null && stack.getItem() == item) count += stack.stackSize;
        return count;
    }

    private static void require(boolean value, String label) {
        Nominator0400Probe.require(value, label);
    }

    private static final class ProbePlayer extends EntityPlayerMP {

        private boolean allowed;

        private ProbePlayer() {
            super(null, null, new GameProfile(Nominator0400Probe.A, "Probe"), null);
        }

        @Override
        public boolean canCommandSenderUseCommand(int level, String command) {
            return allowed;
        }
    }

    private static final class ProbeWorld extends World {

        private Entity target;

        private ProbeWorld() {
            super(null, "Probe", (WorldProvider) null, (WorldSettings) null, null);
        }

        @Override
        public Entity getEntityByID(int id) {
            return target != null && target.getEntityId() == id ? target : null;
        }

        @Override
        protected IChunkProvider createChunkProvider() {
            return null;
        }

        @Override
        protected int func_152379_p() {
            return 0;
        }
    }
}
