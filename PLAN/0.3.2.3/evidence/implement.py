from pathlib import Path
R=Path.cwd()
def edit(p,a,b):
 p=R/p;s=p.read_text(encoding='utf-8');assert a in s,(p,a);p.write_text(s.replace(a,b),encoding='utf-8')
edit('gradle.properties','modVersion = 0.3.2.2','modVersion = 0.3.2.3')
p='src/main/java/darkgrey/rpg/identity/NpcIdentitySavedData.java'
edit(p,'private NpcIdentityRegistry registry', 'private long revision;\n\n    public synchronized long getRevision() { return revision; }\n\n    private NpcIdentityRegistry registry')
edit(p,'if (changed) markDirty();','if (changed) { revision++; markDirty(); }')
p='src/main/java/darkgrey/rpg/item/identity/ItemIdentityRegistry.java'
edit(p,'    public synchronized boolean unbindItem(','''    public synchronized boolean transferItem(String itemId, ItemStackDefinition definition) {
        String id = requireId(itemId, "Item ID");
        if (definition == null) throw new IllegalArgumentException("Definition required.");
        if (definition.equals(items.get(id))) return false;
        items.put(id, definition);
        return true;
    }

    public synchronized boolean releaseGroup(String groupId) {
        return groups.remove(requireId(groupId, "Group ID")) != null;
    }

    public synchronized boolean unbindDefinition(ItemStack stack) {
        boolean changed = false;
        for (String id : matchingItemIds(stack)) changed |= unbindItem(id);
        for (GroupBinding binding : groupBindings()) {
            if (binding.getMember().matches(stack))
                changed |= removeGroupMember(binding.getGroupId(), binding.getMember());
        }
        return changed;
    }

    public synchronized boolean unbindItem(''')
p='src/main/java/darkgrey/rpg/item/identity/ItemIdentitySavedData.java'
edit(p,'    public synchronized boolean unbindItem(','''    public synchronized boolean transferItem(String id, ItemStackDefinition definition) {
        boolean changed = registry.transferItem(id, definition);
        if (changed) { revision++; markDirty(); }
        return changed;
    }

    public synchronized boolean releaseGroup(String id) {
        boolean changed = registry.releaseGroup(id);
        if (changed) { revision++; markDirty(); }
        return changed;
    }

    public synchronized boolean unbindDefinition(ItemStack stack) {
        if (stack == null) throw new IllegalArgumentException("Stack required.");
        boolean changed = registry.unbindDefinition(stack);
        if (changed) { revision++; markDirty(); }
        return changed;
    }

    public synchronized boolean unbindItem(''')
p='src/main/java/darkgrey/rpg/nominator/NominatorService.java'
edit(p,'    private static void clearTransferredHostSelection(','''    public static NominatorResult releaseEntityResource(boolean authorized, String id,
        ProjectSnapshot project, NpcIdentitySavedData identities, NominatorSavedData selections) {
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        ActorDefinition actor = project == null ? null : project.getActor(id);
        if (actor == null) return NominatorResult.rejected("invalid_resource", "角色资源不可用。");
        boolean changed = false;
        synchronized (identities) {
            synchronized (selections) {
                if (actor.isIndividual()) {
                    changed = identities.unbindNpcId(id);
                    for (NominatorEntityBinding b : selections.bindings()) {
                        if (id.equals(b.getIndividualId())) {
                            clearTransferredHostSelection(b.getEntityUuid(), selections);
                            changed = true;
                        }
                    }
                } else {
                    for (NominatorEntityBinding b : selections.bindings()) {
                        List<String> groups = new ArrayList<String>(b.getGroupIds());
                        if (groups.remove(id)) {
                            if (groups.isEmpty() && b.getIndividualId() == null) selections.remove(b.getEntityUuid());
                            else selections.put(new NominatorEntityBinding(b.getEntityUuid(), b.getIndividualId(), groups, b.getStoryId()));
                            changed = true;
                        }
                    }
                    for (String type : selections.typeGroups().keySet()) changed |= selections.removeTypeGroup(type, id);
                }
            }
        }
        return changed ? NominatorResult.accepted("ID已释放，资源保留。") : NominatorResult.noop("ID已经空闲。");
    }

    private static void clearTransferredHostSelection(''')
p='src/main/java/darkgrey/rpg/nominator/NominatorResult.java'
edit(p,'    public static NominatorResult rejected(','''    public static NominatorResult noop(String explanation) {
        return new NominatorResult(true, "noop", explanation);
    }

    public static NominatorResult rejected(''')
# Two independent single-stack slots, retaining TARGET_SLOT compatibility alias.
p='src/main/java/darkgrey/rpg/nominator/container/InventoryNominatorTarget.java'
s=(R/p).read_text();s=s.replace('private ItemStack stack;', 'private final ItemStack[] stacks = new ItemStack[2];').replace('return 1;', 'return 2;',1)
s=s.replace('slot == 0 ? stack : null','slot >= 0 && slot < stacks.length ? stacks[slot] : null').replace('slot != 0','slot < 0 || slot >= stacks.length').replace('slot == 0 &&','slot >= 0 && slot < stacks.length &&').replace('isItemValidForSlot(0, value)','isItemValidForSlot(slot, value)')
import re
s=re.sub(r'\bstack\b', 'stacks[slot]',s)
# parenthesize bounds in combined condition
s=s.replace('if (slot < 0 || slot >= stacks.length ||', 'if (slot < 0 || slot >= stacks.length ||')
(R/p).write_text(s,encoding='utf8')
p='src/main/java/darkgrey/rpg/nominator/container/ContainerNominatorInventory.java'
edit(p,'public static final int TARGET_SLOT = 0;', 'public static final int NOMINATE_SLOT = 0;\n    public static final int UNBIND_SLOT = 1;\n    public static final int TARGET_SLOT = NOMINATE_SLOT;')
edit(p,'private static final int PLAYER_SLOT_START = 1;', 'public static final int PLAYER_SLOT_START = 2;')
edit(p,'private static final int PLAYER_SLOT_END = 37;', 'public static final int PLAYER_SLOT_END = 38;')
edit(p,'addSlotToContainer(new TargetSlot(targetInventory, TARGET_SLOT, TARGET_DEFAULT_X, TARGET_DEFAULT_Y));', 'addSlotToContainer(new TargetSlot(targetInventory, NOMINATE_SLOT, TARGET_DEFAULT_X, TARGET_DEFAULT_Y));\n        addSlotToContainer(new TargetSlot(targetInventory, UNBIND_SLOT, TARGET_DEFAULT_X, TARGET_DEFAULT_Y + 56));')
edit(p,'if (slotIndex == TARGET_SLOT)', 'if (slotIndex < PLAYER_SLOT_START)')
edit(p,'ItemStack target = targetInventory.takeStack();', '''returnSlotToOwner(player, NOMINATE_SLOT);
        returnSlotToOwner(player, UNBIND_SLOT);
    }

    public void returnSlotToOwner(EntityPlayer player, int slot) {
        if (player != owner || player.worldObj == null || player.worldObj.isRemote) return;
        ItemStack target = targetInventory.getStackInSlotOnClosing(slot);''')
