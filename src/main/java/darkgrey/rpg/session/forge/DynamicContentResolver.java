package darkgrey.rpg.session.forge;

import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.server.MinecraftServer;

import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.runtime.DynamicContentText;

/** Server-owned values only; item counts use the same mainInventory scope and identity registry as Task objectives. */
public final class DynamicContentResolver {

    private static long nextWarning;

    private DynamicContentResolver() {}

    public static String resolve(String template, EntityPlayerMP player) {
        if (player == null) return template;
        return resolve(
            template,
            player.getUniqueID(),
            darkgrey.rpg.DarkGreyRpg.getProjectRepository()
                .getSnapshot());
    }

    public static darkgrey.rpg.session.server.CanonicalSessionServerService.TextResolver forProject(
        final ProjectSnapshot project) {
        return new darkgrey.rpg.session.server.CanonicalSessionServerService.TextResolver() {

            @Override
            public String resolve(UUID player, String template) {
                return DynamicContentResolver.resolve(template, player, project);
            }
        };
    }

    public static String resolve(String template, final UUID playerId, final ProjectSnapshot project) {
        if (template == null || !template.startsWith(DynamicContentText.PREFIX))
            return template == null ? "" : template;
        final MinecraftServer server = MinecraftServer.getServer();
        EntityPlayerMP found = null;
        if (server != null && server.getConfigurationManager() != null)
            for (Object entry : server.getConfigurationManager().playerEntityList) {
                EntityPlayerMP player = (EntityPlayerMP) entry;
                if (playerId.equals(player.getUniqueID())) {
                    found = player;
                    break;
                }
            }
        final EntityPlayerMP player = found;
        try {
            return DynamicContentText.resolve(template, new DynamicContentText.Resolver() {

                @Override
                public String resolve(String type, String itemId) {
                    if ("actor_name".equals(type)) return project.getActor(itemId) == null ? "角色引用缺失"
                        : project.getActor(itemId)
                            .getDisplayName();
                    if ("item_name".equals(type)) return project.getItem(itemId) != null ? project.getItem(itemId)
                        .getDisplayName()
                        : project.getItemGroup(itemId) != null ? project.getItemGroup(itemId)
                            .getDisplayName() : "物品引用缺失";
                    if (player == null) return "数据不可用";
                    if ("player_name".equals(type)) return player.getCommandSenderName();
                    if ("player_level".equals(type)) return Integer.toString(player.experienceLevel);
                    if (project.getItem(itemId) == null && project.getItemGroup(itemId) == null) return "物品引用缺失";
                    ItemIdentitySavedData identities = (ItemIdentitySavedData) server
                        .worldServerForDimension(0).mapStorage
                            .loadData(ItemIdentitySavedData.class, ItemIdentitySavedData.DATA_NAME);
                    if (identities == null || identities.getItem(itemId) == null && identities.getGroup(itemId)
                        .isEmpty()) return "数据不可用";
                    long count = 0;
                    for (ItemStack stack : player.inventory.mainInventory) if (stack != null && stack.stackSize > 0
                        && (identities.matchesItem(itemId, stack) || identities.matchesGroup(itemId, stack)))
                        count += stack.stackSize;
                    return Long.toString(count);
                }
            });
        } catch (RuntimeException invalid) {
            long now = System.currentTimeMillis();
            if (now >= nextWarning) {
                nextWarning = now + 60000;
                darkgrey.rpg.DarkGreyRpg.LOG.warn(
                    "Dynamic content could not be resolved; showing unavailable marker (further warnings limited to once per minute).",
                    invalid);
            }
            return "〔动态内容不可用〕";
        }
    }
}
