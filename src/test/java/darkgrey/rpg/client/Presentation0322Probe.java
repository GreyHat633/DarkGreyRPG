package darkgrey.rpg.client;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;

/** Cache ordering, ownership, malformed-input and world lifecycle regression. */
public final class Presentation0322Probe {

    public static void main(String[] args) {
        Object firstWorld = new Object();
        CanonicalTaskClientStore.synchronizeWorld(firstWorld);
        NBTTagCompound first = snapshot(1, "first", 0);
        require(CanonicalTaskClientStore.replace(first), "initial login snapshot");
        first.getTagList("tasks", 10)
            .getCompoundTagAt(0)
            .setString("title", "mutated");
        require(
            "Task".equals(
                CanonicalTaskClientStore.getSnapshot()
                    .getTagList("tasks", 10)
                    .getCompoundTagAt(0)
                    .getString("title")),
            "detach input");
        NBTTagCompound exposed = CanonicalTaskClientStore.getSnapshot();
        exposed.removeTag("tasks");
        require(
            CanonicalTaskClientStore.getSnapshot()
                .hasKey("tasks", 9),
            "detach reads");
        require(!CanonicalTaskClientStore.replace(snapshot(0, "stale", 0)), "reject stale");
        require(!CanonicalTaskClientStore.replace(snapshot(1, "duplicate", 0)), "reject duplicate");
        require(CanonicalTaskClientStore.replace(snapshot(2, "first", 2)), "progress push");
        require(CanonicalTaskClientStore.replace(snapshot(3, "second", 0)), "objective transition push");
        require(
            "second".equals(
                CanonicalTaskClientStore.getSnapshot()
                    .getTagList("tasks", 10)
                    .getCompoundTagAt(0)
                    .getTagList("objectives", 10)
                    .getCompoundTagAt(0)
                    .getString("text")),
            "new objective replaces old");
        NBTTagCompound malformed = snapshot(4, "bad", 0);
        malformed.getTagList("tasks", 10)
            .getCompoundTagAt(0)
            .removeTag("id");
        require(!CanonicalTaskClientStore.replace(malformed), "malformed snapshot rejected atomically");
        require(CanonicalTaskClientStore.getRevision() == 3, "invalid snapshot preserves revision");
        NBTTagCompound wrongList = snapshot(4, "bad list", 0);
        NBTTagList strings = new NBTTagList();
        strings.appendTag(new net.minecraft.nbt.NBTTagString("not a task"));
        wrongList.setTag("tasks", strings);
        require(!CanonicalTaskClientStore.replace(wrongList), "wrong list element type rejected");
        require(CanonicalTaskClientStore.getRevision() == 3, "wrong list preserves cached state");
        NBTTagCompound settled = new NBTTagCompound();
        settled.setLong("revision", 4);
        settled.setTag("tasks", new NBTTagList());
        require(
            CanonicalTaskClientStore.replace(settled) && CanonicalTaskClientStore.getSnapshot()
                .getTagList("tasks", 10)
                .tagCount() == 0,
            "settlement removal");
        CanonicalTaskClientStore.synchronizeWorld(new Object());
        require(CanonicalTaskClientStore.getRevision() == -1, "world change clears revision");
        require(CanonicalTaskClientStore.replace(snapshot(1, "other-server", 0)), "new server revision accepted");
        CanonicalTaskClientStore.synchronizeWorld(null);
        require(
            CanonicalTaskClientStore.getSnapshot()
                .getTagList("tasks", 10)
                .tagCount() == 0,
            "disconnect clears cache");
        CanonicalTaskSavedData source = new CanonicalTaskSavedData();
        long generation = source.getPresentationGeneration();
        source.markDirty();
        require(source.getPresentationGeneration() > generation, "persistent mutation invalidates presentation");
        darkgrey.rpg.client.gui.TaskLayout0322Probe.main(new String[0]);
        narration();
        System.out.println("TASK_CACHE_REVISION_DETACH_TRANSITION_WORLD_CLEAR=PASS");
        System.out.println("TASK_PRESENTATION_DIRTY_GENERATION=PASS");
    }

    private static void narration() {
        darkgrey.rpg.network.message.canonical.CanonicalSessionFrame original = new darkgrey.rpg.network.message.canonical.CanonicalSessionFrame(
            7L,
            "story",
            "session",
            "narration",
            darkgrey.rpg.network.message.canonical.CanonicalSessionFrame.Kind.LINE,
            "",
            "Narration text",
            java.util.Collections.<darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption>emptyList());
        io.netty.buffer.ByteBuf bytes = io.netty.buffer.Unpooled.buffer();
        original.toBytes(bytes);
        darkgrey.rpg.network.message.canonical.CanonicalSessionFrame decoded = new darkgrey.rpg.network.message.canonical.CanonicalSessionFrame();
        decoded.fromBytes(bytes);
        bytes.release();
        darkgrey.rpg.client.session.CanonicalSessionClientModel model = new darkgrey.rpg.client.session.CanonicalSessionClientModel();
        require(
            model.acceptFrame(decoded) && model.getFrame()
                .canContinue(),
            "narration transport accepts continue");
        require(
            "narration".equals(
                model.continueAction()
                    .getCurrentNodeId()),
            "narration action keeps node identity");
        model.clear();
        require(
            !model.isActive() && model.getVisibleText()
                .isEmpty(),
            "world clear discards narration context");
        System.out.println("NARRATION_TRANSPORT_CONTINUE_WORLD_CLEAR=PASS");
    }

    private static NBTTagCompound snapshot(long revision, String text, int progress) {
        NBTTagCompound root = new NBTTagCompound();
        root.setLong("revision", revision);
        NBTTagCompound task = new NBTTagCompound();
        task.setString("id", "task");
        task.setString("title", "Task");
        NBTTagCompound objective = new NBTTagCompound();
        objective.setString("text", text);
        objective.setInteger("current", progress);
        objective.setInteger("required", 3);
        NBTTagList objectives = new NBTTagList();
        objectives.appendTag(objective);
        task.setTag("objectives", objectives);
        NBTTagList tasks = new NBTTagList();
        tasks.appendTag(task);
        root.setTag("tasks", tasks);
        return root;
    }

    private static void require(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }
}
