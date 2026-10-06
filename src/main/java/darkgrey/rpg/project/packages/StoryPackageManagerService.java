package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.UUID;
import java.util.WeakHashMap;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;

/** Server-owned sessions, opaque source handles and revision-checked whole-container operations. */
public final class StoryPackageManagerService {

    public static final int OPEN = 0, LIST = 1, DETAILS = 2, TOGGLE = 3, RESCAN = 4, PREPARE_DELETE = 5,
        CONFIRM_DELETE = 6, GRAPH = 7, CLOSE = 8;
    public static final int PAGE_SIZE = 12, GRAPH_PAGE_SIZE = 32;
    private final StoryPackageLoader loader;
    private final RuntimeAccess runtime;
    private final Map<Object, Session> sessions = new WeakHashMap<Object, Session>();

    public interface RuntimeAccess {

        void reload();

        void retire(Map<String, LoadedStoryPackage> retained);

        int activeCount(Set<String> stories);
    }

    public StoryPackageManagerService(StoryPackageLoader loader, RuntimeAccess runtime) {
        this.loader = Objects.requireNonNull(loader);
        this.runtime = Objects.requireNonNull(runtime);
    }

    public synchronized NBTTagCompound request(Object owner, boolean allowed, long sequence, NBTTagCompound input) {
        NBTTagCompound output = new NBTTagCompound();
        if (!allowed) {
            sessions.remove(owner);
            output.setBoolean("denied", true);
            output.setString("error", "管理员权限已失效。");
            return output;
        }
        try {
            if (!input.hasKey("action", 3) || sequence <= 0) throw new IllegalArgumentException("无效管理请求。");
            int action = input.getInteger("action");
            Session session = sessions.get(owner);
            if (action == OPEN) {
                if (session != null && sequence <= session.sequence) throw new IllegalArgumentException("过期请求。");
                session = new Session();
                sessions.put(owner, session);
            } else if (session == null || !session.id.equals(input.getString("session"))) {
                sessions.remove(owner);
                output.setBoolean("closed", true);
                throw new IllegalArgumentException("管理会话已失效，请重新打开。");
            }
            if (sequence <= session.sequence) throw new IllegalArgumentException("重复或过期请求。");
            session.sequence = sequence;
            output.setString("session", session.id);
            if (action == CLOSE) {
                sessions.remove(owner);
                output.setBoolean("closed", true);
                return output;
            }
            long revision = loader.getInventoryRevision();
            if (action != OPEN && (!input.hasKey("revision", 4) || input.getLong("revision") != revision)) {
                session.confirmation = null;
                output.setBoolean("refresh", true);
                throw new IllegalArgumentException("清单已经变化，请刷新后重试。");
            }
            if (action == OPEN || action == LIST || action == RESCAN) {
                if (action == OPEN || action == RESCAN) runtime.reload();
                list(output, session, page(input));
                return output;
            }
            String handle = input.getString("handle");
            String source = session.handles.get(handle);
            if (source == null || session.revision != revision) throw new IllegalArgumentException("来源已失效，请刷新。");
            StoryPackageInventoryEntry row = null;
            for (StoryPackageInventoryEntry candidate : loader.getInventory()) if (candidate.getSourceName()
                .equals(source)) row = candidate;
            if (row == null) throw new IllegalArgumentException("容器已移除。");
            if (action == TOGGLE) {
                if (!input.hasKey("enabled", 1)) throw new IllegalArgumentException("缺少启停状态。");
                loader.setEnabled(source, input.getBoolean("enabled"), revision);
                runtime.reload();
                list(output, session, page(input));
            } else if (action == PREPARE_DELETE) {
                session.confirmation = UUID.randomUUID()
                    .toString();
                session.confirmSource = source;
                session.confirmHash = loader.deletionFingerprint(source, revision);
                session.confirmedRevision = revision;
                output.setString("confirmation", session.confirmation);
                output.setString("warning", "删除整个容器，并停止其中所有活动故事。完成历史和奖励收据保留。");
                output.setInteger("active", runtime.activeCount(row.getStoryUids()));
                output.setString("name", text(row.getDisplayName()));
            } else if (action == CONFIRM_DELETE) {
                if (session.confirmation == null || !session.confirmation.equals(input.getString("confirmation"))
                    || !source.equals(session.confirmSource)
                    || session.confirmedRevision != revision) throw new IllegalArgumentException("删除确认已过期。");
                session.confirmation = null;
                try {
                    loader.deleteContainer(source, revision, session.confirmHash, runtime::retire);
                } finally {
                    // A failed physical deletion can restore its source. Publish that actual
                    // inventory as well; never leave repository and manager at different sets.
                    runtime.reload();
                }
                list(output, session, page(input));
                output.setString("message", "容器已删除。");
            } else if (action == DETAILS || action == GRAPH) {
                output.setTag("detail", row(row, handle));
                output.setInteger("active", runtime.activeCount(row.getStoryUids()));
                int page = page(input);
                List<String> uids = new ArrayList<String>(row.getStoryUids());
                NBTTagList members = new NBTTagList();
                for (int i = page * GRAPH_PAGE_SIZE; i < Math.min(uids.size(), (page + 1) * GRAPH_PAGE_SIZE); i++) {
                    String uid = uids.get(i);
                    StoryPackageMemberInfo info = row.getMembers()
                        .get(uid);
                    NBTTagCompound member = new NBTTagCompound();
                    member.setString("uid", uid);
                    member.setString("name", info == null ? "故事信息不可用" : text(info.name));
                    if (info != null) {
                        member.setString("repeat", info.repeatPolicy);
                        member.setString("start_rule", text(info.startRule));
                        member.setInteger("owned", info.ownedCount);
                        member.setInteger("references", info.referenceCount);
                        member.setString("version", text(info.version));
                        member.setString("fingerprint", info.fingerprint.substring(0, 12));
                    }
                    members.appendTag(member);
                }
                output.setTag("members", members);
                output.setInteger("member_total", uids.size());
                output.setInteger("page", page);
                NBTTagList diagnostics = new NBTTagList();
                List<String> issues = new ArrayList<String>(row.getConflicts());
                issues.addAll(row.getErrors());
                for (int i = page * PAGE_SIZE; i < Math.min(issues.size(), (page + 1) * PAGE_SIZE); i++)
                    diagnostics.appendTag(new net.minecraft.nbt.NBTTagString(text(issues.get(i))));
                output.setTag("diagnostics", diagnostics);
                output.setInteger("diagnostic_total", issues.size());
                if (action == GRAPH) {
                    NBTTagList edges = new NBTTagList();
                    List<CanonicalStoryLogicConnection> graph = row.getGraph()
                        .getConnections();
                    for (int i = page * GRAPH_PAGE_SIZE; i
                        < Math.min(graph.size(), (page + 1) * GRAPH_PAGE_SIZE); i++) {
                        CanonicalStoryLogicConnection edge = graph.get(i);
                        NBTTagCompound value = new NBTTagCompound();
                        value.setString("from", edge.getSourceStoryId());
                        value.setString("to", edge.getTargetStoryId());
                        value.setString(
                            "kind",
                            edge.getInterfaceKind()
                                .name());
                        edges.appendTag(value);
                    }
                    output.setTag("edges", edges);
                    output.setInteger("edge_total", graph.size());
                }
            } else throw new IllegalArgumentException("未知管理操作。");
        } catch (Exception failure) {
            output.setString("error", text(failure.getMessage() == null ? "管理操作失败。" : failure.getMessage()));
            if (output.getLong("revision") != loader.getInventoryRevision()) output.setBoolean("refresh", true);
        } finally {
            output.setLong("revision", loader.getInventoryRevision());
        }
        return output;
    }

    public synchronized void forget(Object owner) {
        sessions.remove(owner);
    }

    /** Version-only notification. Reads the committed in-memory inventory; never scans. */
    public synchronized NBTTagCompound notification(Object owner, boolean allowed) {
        Session session = sessions.get(owner);
        if (session == null) return null;
        if (!allowed) {
            sessions.remove(owner);
            NBTTagCompound denied = new NBTTagCompound();
            denied.setBoolean("notification", true);
            denied.setBoolean("denied", true);
            return denied;
        }
        long revision = loader.getInventoryRevision();
        if (session.notifiedRevision == revision) return null;
        session.notifiedRevision = revision;
        NBTTagCompound data = new NBTTagCompound();
        data.setBoolean("notification", true);
        data.setString("session", session.id);
        data.setLong("revision", revision);
        return data;
    }

    private void list(NBTTagCompound output, Session session, int page) {
        if (session.revision != loader.getInventoryRevision()) {
            java.util.Set<String> sources = new java.util.HashSet<String>();
            for (StoryPackageInventoryEntry entry : loader.getInventory()) sources.add(entry.getSourceName());
            session.handles.entrySet()
                .removeIf(entry -> !sources.contains(entry.getValue()));
            session.confirmation = null;
            session.revision = loader.getInventoryRevision();
            for (StoryPackageInventoryEntry row : loader.getInventory())
                if (!session.handles.containsValue(row.getSourceName())) session.handles.put(
                    UUID.randomUUID()
                        .toString(),
                    row.getSourceName());
        }
        List<StoryPackageInventoryEntry> inventory = loader.getInventory();
        NBTTagList rows = new NBTTagList();
        int[] counts = new int[4];
        for (StoryPackageInventoryEntry row : inventory) counts[row.getState()
            .ordinal()]++;
        for (int i = page * PAGE_SIZE; i < Math.min(inventory.size(), (page + 1) * PAGE_SIZE); i++) {
            StoryPackageInventoryEntry row = inventory.get(i);
            String handle = null;
            for (Map.Entry<String, String> entry : session.handles.entrySet()) if (entry.getValue()
                .equals(row.getSourceName())) {
                    handle = entry.getKey();
                    break;
                }
            rows.appendTag(row(row, handle));
        }
        output.setTag("rows", rows);
        output.setInteger("page", page);
        output.setInteger("total", inventory.size());
        output.setIntArray("counts", counts);
        session.notifiedRevision = session.revision;
        output.setLong("revision", session.revision);
    }

    private static NBTTagCompound row(StoryPackageInventoryEntry row, String handle) {
        NBTTagCompound value = new NBTTagCompound();
        value.setString("handle", handle);
        value.setString("name", text(row.getDisplayName()));
        value.setString("file", text(row.getSourceName()));
        value.setString(
            "state",
            row.getState()
                .name());
        value.setBoolean("enabled", row.isUserEnabled());
        value.setBoolean(
            "group",
            row.getSourceName()
                .toLowerCase(java.util.Locale.ROOT)
                .endsWith(".dgrs.g"));
        value.setInteger(
            "members",
            row.getStoryUids()
                .size());
        return value;
    }

    private static int page(NBTTagCompound input) {
        int page = input.getInteger("page");
        if (page < 0 || page > 4096) throw new IllegalArgumentException("分页超出范围。");
        return page;
    }

    private static String text(String value) {
        return value == null ? "" : value.substring(0, Math.min(value.length(), 256));
    }

    private static final class Session {

        final String id = UUID.randomUUID()
            .toString();
        final Map<String, String> handles = new LinkedHashMap<String, String>();
        long sequence, revision = -1, notifiedRevision = -1, confirmedRevision;
        String confirmation, confirmSource, confirmHash;
    }
}
