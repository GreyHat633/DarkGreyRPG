package darkgrey.rpg.client.gui;

import java.io.File;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Runs the actual isolated namespaced acceptance package without altering Task Runtime. */
public final class Objective0324Probe {

    public static void main(String[] args) throws Exception {
        StoryPackageLoader loader = new StoryPackageLoader(new File(args[0]));
        StoryPackageLoader.ReloadResult result = loader.load();
        require(result.isSuccessful(), "archive validation: " + result.getErrors());
        LoadedStoryPackage pack = loader.getPackage("GreyHat_:test_story");
        require(pack != null, "actual namespaced test story");
        CanonicalGraphResource task = pack.getSnapshot()
            .getCanonicalTask("GreyHat_:KillSlimes");
        String kill = "node_cfc0a454a1ef4168890d4ce6fbb3e803";
        String interact = "node_4feb6ff8c4944e078f9914cd808c9f17";
        CanonicalGraphNode authored = null;
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes())
            if (node.getId()
                .equals(interact)) authored = node;
        require(
            authored != null && "与酒馆老板对话".equals(
                authored.getProperties()
                    .get("description")
                    .getAsString()),
            "authored interact description");
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(task);
        require(
            runtime.getObjectiveStatuses()
                .get(kill) == CanonicalTaskObjectiveStatus.ACTIVE,
            "kill active");
        require(
            runtime.getObjectiveStatuses()
                .get(interact) != CanonicalTaskObjectiveStatus.ACTIVE,
            "interact gated");
        for (int i = 0; i < 3; i++) runtime.applyEvent(CanonicalTaskEvent.killEntity("GreyHat_:Slimes"));
        require(
            runtime.getObjectiveStatuses()
                .get(kill) == CanonicalTaskObjectiveStatus.COMPLETED,
            "kill completed");
        require(
            runtime.getObjectiveStatuses()
                .get(interact) == CanonicalTaskObjectiveStatus.ACTIVE,
            "interact active");
        runtime.applyEvent(CanonicalTaskEvent.interactActor("GreyHat_:TarvenBoss"));
        require(runtime.isSettled(), "original settlement edge retained");
        if (args.length > 1) {
            File directory = new File(args[1]);
            net.minecraft.nbt.NBTTagCompound saved;
            try (java.io.FileInputStream input = new java.io.FileInputStream(
                new File(directory, "darkgrey_rpg_canonical_tasks.dat"))) {
                saved = net.minecraft.nbt.CompressedStreamTools.readCompressed(input)
                    .getCompoundTag("data");
            }
            java.util.List<darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot> snapshots = darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec
                .decode(saved);
            require(snapshots.size() == 1, "actual world task count preserved");
            CanonicalTaskRuntime restored = CanonicalTaskRuntime.restore(
                task,
                snapshots.get(0)
                    .getRuntimeSnapshot());
            require(
                restored.getObjectiveStatuses()
                    .get(kill) == CanonicalTaskObjectiveStatus.COMPLETED,
                "saved kill stays complete");
            require(
                restored.getObjectiveStatuses()
                    .get(interact) == CanonicalTaskObjectiveStatus.ACTIVE,
                "saved next objective stays active");
            restored.applyEvent(CanonicalTaskEvent.interactActor("GreyHat_:TarvenBoss"));
            require(restored.isSettled(), "saved task can finish without repeating kills");
            try (java.io.FileInputStream input = new java.io.FileInputStream(
                new File(directory, "darkgrey_rpg_story_package_generations.dat"))) {
                net.minecraft.nbt.NBTTagCompound generation = net.minecraft.nbt.CompressedStreamTools
                    .readCompressed(input)
                    .getCompoundTag("data")
                    .getTagList("generations", 10)
                    .getCompoundTagAt(0);
                require(
                    pack.getContentFingerprint()
                        .equals(generation.getString("content_fingerprint")),
                    "generation does not retire saved task");
            }
            System.out.println("USER_SAVE_DESCRIPTION_MIGRATION=PASS");
        }
        System.out.println("RUNTIME_TRANSITION=PASS");
        System.out.println("AUTHOR_DESCRIPTION_FIXED=PASS_ISOLATED_PACKAGE");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
