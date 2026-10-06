package darkgrey.rpg.project.packages;

import java.io.File;

import darkgrey.rpg.project.ProjectLoadException;

public final class StoryGroupPackageProbe {

    public static void main(String[] args) throws Exception {
        darkgrey.rpg.identity.CurrentWorldIdentityProbe.run();
        StoryGroupPackageReader.Result result = StoryGroupPackageReader.read(DgrsArchiveReader.open(new File(args[0])));
        if (result.getManifests()
            .size() != 2
            || result.getConnections()
                .getConnections()
                .size() != 1)
            throw new AssertionError("Studio Group member/edge count changed");
        com.google.gson.JsonObject fingerprints = new com.google.gson.JsonParser()
            .parse(
                new String(
                    java.nio.file.Files.readAllBytes(java.nio.file.Paths.get(args[0] + ".fingerprints.json")),
                    java.nio.charset.StandardCharsets.UTF_8))
            .getAsJsonObject();
        for (String uid : result.getManifests()
            .keySet()) {
            String actual = StoryPackageContentFingerprint.compute(
                result.getManifests()
                    .get(uid),
                result.getSnapshots()
                    .get(uid)
                    .getDeclaredBytes(),
                result.getConnections());
            if (!actual.equals(
                fingerprints.get(uid)
                    .getAsString()))
                throw new AssertionError("C#/Java member fingerprint differs: " + uid);
        }
        for (StoryPackageSnapshotReader.Result member : result.getSnapshots()
            .values()) {
            if (member.getSnapshot()
                .getCanonicalStories()
                .size() != 1) throw new AssertionError("Group replaced the member runtime boundary");
            for (darkgrey.rpg.graph.canonical.CanonicalGraphResource story : member.getSnapshot()
                .getCanonicalStories()
                .values()) {
                for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : story.getGraph()
                    .getNodes()) {
                    if (!"start".equals(node.getType())) continue;
                    for (darkgrey.rpg.graph.canonical.CanonicalGraphPort port : node.getPorts()) {
                        if (port.getDirection() == darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection.OUTPUT
                            && port.getKind() == darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW) {
                            darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntime.start(
                                story,
                                port.getId(),
                                darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy.ONCE);
                            break;
                        }
                    }
                }
            }
            for (darkgrey.rpg.graph.canonical.CanonicalGraphResource task : member.getSnapshot()
                .getCanonicalTasks()
                .values()) {
                darkgrey.rpg.task.runtime.CanonicalTaskRuntime.start(task);
            }
        }
        for (int i = 1; i < args.length; i++) {
            try {
                StoryGroupPackageReader.read(DgrsArchiveReader.open(new File(args[i])));
                throw new AssertionError("Malformed Group accepted: " + args[i]);
            } catch (ProjectLoadException expected) {}
        }
        java.nio.file.Path probe = java.nio.file.Files.createTempDirectory(
            new File(args[0]).getAbsoluteFile()
                .getParentFile()
                .toPath(),
            "inventory-");
        java.nio.file.Path installed = probe.resolve("StoryPackages");
        java.nio.file.Files.createDirectory(installed);
        java.nio.file.Path groupPath = installed.resolve("group.dgrs.g");
        java.nio.file.Files.copy(new File(args[0]).toPath(), groupPath);
        StoryPackageLoader loader = new StoryPackageLoader(
            installed.toFile(),
            probe.resolve("Cache")
                .toFile());
        if (!loader.reload()
            .isSuccessful()
            || loader.getPackages()
                .size() != 2)
            throw new AssertionError(
                "Group scanner failed: " + loader.getLastReload()
                    .getErrors());
        StoryPackageSnapshotMerger.merge(loader.getPackages());
        managerContract(loader, groupPath, new File(args[0]).toPath());
        retainedHistory(StoryPackageSnapshotMerger.merge(loader.getPackages()));
        String uid = result.getManifests()
            .keySet()
            .iterator()
            .next();
        LoadedStoryPackage retained = loader.getPackage(uid);
        loader.setEnabled("group.dgrs.g", false, loader.getInventoryRevision());
        darkgrey.rpg.session.persistence.CanonicalSessionSavedData runtimeData = new darkgrey.rpg.session.persistence.CanonicalSessionSavedData();
        darkgrey.rpg.story.canonical.server.CanonicalStoryServerService service = new darkgrey.rpg.story.canonical.server.CanonicalStoryServerService(
            StoryPackageSnapshotMerger.merge(loader.getPackages()),
            runtimeData,
            loader::allowsNewStart);
        java.util.UUID player = java.util.UUID.randomUUID();
        if (service.startByRegion(player, uid, 0, 0, 0, 0, 1) != null
            || runtimeData.getStorySnapshot(player, uid) != null
            || !service.eligibleStartStoryIds(player)
                .isEmpty())
            throw new AssertionError("Disabled container started a Story runtime");
        if (loader.allowsNewStart(uid) || loader.getPackage(uid) != retained)
            throw new AssertionError("Disable retired an unchanged member or permits a new Start");
        java.nio.file.Path single = new File(args[0]).getAbsoluteFile()
            .getParentFile()
            .getParentFile()
            .toPath()
            .resolve("Packages/consumer.dgrs");
        java.nio.file.Files.copy(single, installed.resolve("duplicate.dgrs"));
        darkgrey.rpg.project.ProjectRepository repository = new darkgrey.rpg.project.ProjectRepository(
            probe.resolve("Project")
                .toFile());
        repository.installSnapshot(StoryPackageSnapshotMerger.merge(loader.getPackages()));
        StoryPackageRuntimeReloader.reload(repository, loader);
        if (!repository.getSnapshot()
            .getCanonicalStories()
            .isEmpty()) throw new AssertionError("Conflict retained the prior repository snapshot");
        if (!loader.getPackages()
            .isEmpty()
            || loader.getInventory()
                .size() != 2
            || loader.getInventory()
                .stream()
                .anyMatch(entry -> entry.getState() != StoryPackageInventoryEntry.State.CONFLICT))
            throw new AssertionError("Disabled Group and duplicate single did not both conflict");
        java.nio.file.Files.delete(installed.resolve("duplicate.dgrs"));
        loader.reload();
        if (loader.getPackages()
            .size() != 2 || loader.allowsNewStart(uid))
            throw new AssertionError("Conflict recovery changed the disabled preference");
        if (args.length > 1) {
            java.nio.file.Files
                .copy(new File(args[1]).toPath(), groupPath, java.nio.file.StandardCopyOption.REPLACE_EXISTING);
            java.nio.file.Files.copy(single, installed.resolve("duplicate.dgrs"));
            loader.reload();
            if (!loader.getPackages()
                .isEmpty() || loader.getInventory()
                    .stream()
                    .noneMatch(
                        entry -> entry.getState() == StoryPackageInventoryEntry.State.ERROR && !entry.getConflicts()
                            .isEmpty()))
                throw new AssertionError("Invalid payload lost known claims or retained old running content");
        }
        corruptStoredEntry(new File(args[0]), groupPath, "project.json");
        loader.reload();
        if (!loader.getPackages()
            .isEmpty() || loader.getInventory()
                .stream()
                .noneMatch(
                    entry -> entry.getState() == StoryPackageInventoryEntry.State.ERROR && !entry.getStoryUids()
                        .isEmpty()
                        && !entry.getConflicts()
                            .isEmpty()))
            throw new AssertionError("Bad payload CRC lost valid manifest claims");
        corruptStoredEntry(new File(args[0]), groupPath, "manifest.json");
        loader.reload();
        if (loader.getPackages()
            .size() != 1 || loader.getInventory()
                .stream()
                .noneMatch(
                    entry -> entry.getState() == StoryPackageInventoryEntry.State.ERROR && entry.getStoryUids()
                        .isEmpty()))
            throw new AssertionError("Corrupt manifest supplied trusted identity claims");
        java.nio.file.Files.write(groupPath, new byte[] { 1, 2, 3 });
        loader.reload();
        if (loader.getPackages()
            .size() != 1 || loader.getInventory()
                .stream()
                .noneMatch(
                    entry -> entry.getState() == StoryPackageInventoryEntry.State.ERROR && entry.getStoryUids()
                        .isEmpty()))
            throw new AssertionError("Unreadable manifest guessed UIDs or disabled unrelated valid content");
        java.nio.file.Files.delete(groupPath);
        java.nio.file.Files.delete(installed.resolve("duplicate.dgrs"));
        loader.reload();
        System.out.println(
            "PASS: Studio flat Group, complete 2-member validation, shared resources, 1 edge; rejected "
                + (args.length - 1)
                + " malformed containers.");
    }

    private static void corruptStoredEntry(File source, java.nio.file.Path target, String entryName) throws Exception {
        try (java.util.zip.ZipFile input = new java.util.zip.ZipFile(source);
            java.util.zip.ZipOutputStream output = new java.util.zip.ZipOutputStream(
                java.nio.file.Files.newOutputStream(target))) {
            java.util.Enumeration<? extends java.util.zip.ZipEntry> entries = input.entries();
            while (entries.hasMoreElements()) {
                java.util.zip.ZipEntry entry = entries.nextElement();
                java.io.ByteArrayOutputStream content = new java.io.ByteArrayOutputStream();
                try (java.io.InputStream stream = input.getInputStream(entry)) {
                    byte[] buffer = new byte[8192];
                    int count;
                    while ((count = stream.read(buffer)) != -1) content.write(buffer, 0, count);
                }
                byte[] bytes = content.toByteArray();
                java.util.zip.CRC32 crc = new java.util.zip.CRC32();
                crc.update(bytes);
                java.util.zip.ZipEntry stored = new java.util.zip.ZipEntry(entry.getName());
                stored.setMethod(java.util.zip.ZipEntry.STORED);
                stored.setSize(bytes.length);
                stored.setCrc(crc.getValue());
                output.putNextEntry(stored);
                output.write(bytes);
                output.closeEntry();
            }
        }
        byte[] archive = java.nio.file.Files.readAllBytes(target);
        java.nio.ByteBuffer header = java.nio.ByteBuffer.wrap(archive)
            .order(java.nio.ByteOrder.LITTLE_ENDIAN);
        int offset = 0;
        while (header.getInt(offset) == 0x04034b50) {
            int nameLength = header.getShort(offset + 26) & 0xffff;
            int extraLength = header.getShort(offset + 28) & 0xffff;
            int payload = offset + 30 + nameLength + extraLength;
            String name = new String(archive, offset + 30, nameLength, java.nio.charset.StandardCharsets.UTF_8);
            if (name.equals(entryName)) {
                archive[payload] ^= 1;
                java.nio.file.Files.write(target, archive);
                return;
            }
            offset = payload + header.getInt(offset + 18);
        }
        throw new AssertionError("Missing CRC fixture entry: " + entryName);
    }

    private static void managerContract(StoryPackageLoader loader, java.nio.file.Path installed,
        java.nio.file.Path original) throws Exception {
        int[] retirements = { 0 };
        StoryPackageManagerService manager = new StoryPackageManagerService(
            loader,
            new StoryPackageManagerService.RuntimeAccess() {

                @Override
                public void reload() {
                    loader.reload();
                }

                @Override
                public void retire(java.util.Map<String, LoadedStoryPackage> retained) {
                    if (!retained.isEmpty() || !java.nio.file.Files.exists(installed)) throw new AssertionError(
                        "Deletion did not retire the complete Group before touching its source");
                    retirements[0]++;
                }

                @Override
                public int activeCount(java.util.Set<String> stories) {
                    return 2;
                }
            });
        Object owner = new Object();
        net.minecraft.nbt.NBTTagCompound input = new net.minecraft.nbt.NBTTagCompound();
        input.setInteger("action", StoryPackageManagerService.OPEN);
        net.minecraft.nbt.NBTTagCompound opened = manager.request(owner, true, 1, input);
        if (!opened.hasKey("rows", 9) || opened.getTagList("rows", 10)
            .tagCount() != 1) throw new AssertionError("Manager open omitted Group");
        String handle = opened.getTagList("rows", 10)
            .getCompoundTagAt(0)
            .getString("handle");
        input.setString("session", opened.getString("session"));
        input.setLong("revision", opened.getLong("revision"));
        input.setString("handle", handle);
        input.setInteger("action", StoryPackageManagerService.GRAPH);
        net.minecraft.nbt.NBTTagCompound graph = manager.request(owner, true, 2, input);
        if (graph.getTagList("members", 10)
            .tagCount() != 2
            || graph.getTagList("edges", 10)
                .tagCount() != 1)
            throw new AssertionError("Bounded graph projection lost members or edges");
        if (graph.toString()
            .contains("graph\":")
            || graph.toString()
                .contains("media/"))
            throw new AssertionError("Manager leaked full resource definitions");
        for (int action = 0; action <= 9; action++) {
            input.setInteger("action", action);
            net.minecraft.nbt.NBTTagCompound denied = manager.request(new Object(), false, action + 1, input);
            if (!denied.getBoolean("denied") || denied.hasKey("rows") || denied.hasKey("detail"))
                throw new AssertionError("Non-OP manager request was not denied: " + action);
        }
        input.setInteger("action", StoryPackageManagerService.PREPARE_DELETE);
        net.minecraft.nbt.NBTTagCompound confirmation = manager.request(owner, true, 3, input);
        if (confirmation.getInteger("active") != 2 || !confirmation.hasKey("confirmation", 8))
            throw new AssertionError("Missing whole-container deletion confirmation");
        input.setString("confirmation", confirmation.getString("confirmation"));
        input.setInteger("action", StoryPackageManagerService.CONFIRM_DELETE);
        if (!manager.request(new Object(), true, 4, input)
            .getBoolean("closed")) throw new AssertionError("Foreign session reused a delete token");
        loader.setEnabled("group.dgrs.g", false, loader.getInventoryRevision());
        if (!manager.request(owner, true, 4, input)
            .getBoolean("refresh") || !java.nio.file.Files.exists(installed) || retirements[0] != 0)
            throw new AssertionError("Stale revision deleted the source");
        input.setInteger("action", StoryPackageManagerService.LIST);
        input.setLong("revision", loader.getInventoryRevision());
        opened = manager.request(owner, true, 5, input);
        input.setString(
            "handle",
            opened.getTagList("rows", 10)
                .getCompoundTagAt(0)
                .getString("handle"));
        input.setInteger("action", StoryPackageManagerService.PREPARE_DELETE);
        confirmation = manager.request(owner, true, 6, input);
        input.setString("confirmation", confirmation.getString("confirmation"));
        input.setInteger("action", StoryPackageManagerService.CONFIRM_DELETE);
        net.minecraft.nbt.NBTTagCompound deleted = manager.request(owner, true, 7, input);
        if (deleted.hasKey("error") || java.nio.file.Files.exists(installed)
            || !loader.getPackages()
                .isEmpty()
            || retirements[0] != 1) throw new AssertionError("Whole-container deletion failed: " + deleted);
        if (!manager.request(owner, true, 7, input)
            .hasKey("error")) throw new AssertionError("Replayed deletion sequence accepted");
        java.nio.file.Files.copy(original, installed);
        loader.reload();
        if (loader.getPackages()
            .size() != 2) throw new AssertionError("Manager fixture restoration failed");
        manager.request(owner, false, 8, input);
        if (!manager.request(owner, true, 9, input)
            .getBoolean("closed")) throw new AssertionError("Revoked session remained usable");
    }

    private static void retainedHistory(darkgrey.rpg.project.ProjectSnapshot project) {
        String uid = project.getCanonicalStories()
            .keySet()
            .iterator()
            .next();
        java.util.UUID player = java.util.UUID.randomUUID();
        net.minecraft.world.storage.MapStorage storage = new net.minecraft.world.storage.MapStorage(null);
        darkgrey.rpg.session.persistence.CanonicalSessionSavedData data = darkgrey.rpg.session.persistence.CanonicalSessionSavedData
            .get(storage);
        darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot runtime = new darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot(
            uid,
            "retired-fingerprint",
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus.TERMINATED,
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy.ONCE,
            "old-trigger",
            null,
            null,
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.NONE,
            null,
            java.util.Collections.<String, Boolean>emptyMap(),
            null);
        darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot completed = new darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot(
            player,
            uid,
            1,
            2L,
            runtime);
        data.readFromNBT(
            darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec.encode(
                java.util.Collections.emptyList(),
                1,
                java.util.Collections.emptyList(),
                java.util.Collections.singletonList(completed)));
        data.discardByStoryIds(java.util.Collections.singleton(uid));
        darkgrey.rpg.story.canonical.server.CanonicalStoryServerService service = new darkgrey.rpg.story.canonical.server.CanonicalStoryServerService(
            project,
            data);
        if (data.getStorySnapshot(player, uid) != null || data.completedStorySummary(player, uid) == null
            || service.startDisposition(player, uid)
                != darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.ONCE_TERMINAL
            || service.startByRegion(player, uid, 0, 0, 0, 0, 3) != null)
            throw new AssertionError("Retirement erased Story completion or replayed its old cursor");
        darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory history = darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory
            .get(storage);
        net.minecraft.nbt.NBTTagCompound saved = new net.minecraft.nbt.NBTTagCompound();
        history.writeToNBT(saved);
        darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory restored = new darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory();
        restored.readFromNBT(saved);
        if (!history.summary(player, uid)
            .equals(restored.summary(player, uid)))
            throw new AssertionError("Completion history restart changed summary");
        net.minecraft.nbt.NBTTagCompound corrupt = (net.minecraft.nbt.NBTTagCompound) saved.copy();
        net.minecraft.nbt.NBTTagList wrong = new net.minecraft.nbt.NBTTagList();
        wrong.appendTag(new net.minecraft.nbt.NBTTagString("wrong-type"));
        corrupt.setTag("summaries", wrong);
        try {
            restored.readFromNBT(corrupt);
            throw new AssertionError("Wrong history list type accepted");
        } catch (IllegalArgumentException expected) {}
        if (!history.summary(player, uid)
            .equals(restored.summary(player, uid)))
            throw new AssertionError("Rejected history replaced previous summaries");
        restored.observe(completed);
        if (restored.summary(player, uid)
            .getLong("completed_count") != 1)
            throw new AssertionError("Repeated retirement counted a completion twice");
        java.util.UUID interruptedPlayer = java.util.UUID.randomUUID();
        darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot active = new darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot(
            uid,
            "old-active-fingerprint",
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus.ACTIVE,
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy.REPEATABLE,
            "old-trigger",
            "old-node",
            "flow_in",
            darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.NONE,
            null,
            java.util.Collections.<String, Boolean>emptyMap(),
            null);
        data.readFromNBT(
            darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec.encode(
                java.util.Collections.emptyList(),
                1,
                java.util.Collections.emptyList(),
                java.util.Collections.singletonList(
                    new darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot(
                        interruptedPlayer,
                        uid,
                        3,
                        null,
                        active))));
        data.discardByStoryIds(java.util.Collections.singleton(uid));
        service = new darkgrey.rpg.story.canonical.server.CanonicalStoryServerService(project, data);
        if (service.startDisposition(interruptedPlayer, uid)
            != darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.ERROR_TERMINAL
            || service.startByRegion(interruptedPlayer, uid, 0, 0, 0, 0, 4) != null
            || data.completedStorySummary(interruptedPlayer, uid)
                .getLong("completed_count") != 0)
            throw new AssertionError("Interrupted run automatically replayed after container recovery");
        data.discardByPlayerStory(interruptedPlayer, uid);
        if (service.startDisposition(interruptedPlayer, uid)
            != darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.NEW
            || data.completedStorySummary(interruptedPlayer, uid)
                .getLong("completed_count") != 0)
            throw new AssertionError("Explicit reset did not reopen admission or erased history");
        data.discardByPlayerStory(player, uid);
        if (service.startDisposition(player, uid)
            != darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.NEW
            || data.completedStorySummary(player, uid)
                .getLong("completed_count") != 1)
            throw new AssertionError("Reset lost an existing completion count");
        history.writeToNBT(saved);
        restored.readFromNBT(saved);
        if (restored.disposition(player, uid, project.getCanonicalStory(uid), java.time.Clock.systemUTC())
            != darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition.NEW
            || restored.summary(player, uid)
                .getLong("completed_count") != 1)
            throw new AssertionError("Explicit reset admission did not survive restart");
    }
}
