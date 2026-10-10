package darkgrey.rpg.persistence;

import net.minecraft.launchwrapper.IClassTransformer;

import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.Opcodes;
import org.objectweb.asm.tree.AbstractInsnNode;
import org.objectweb.asm.tree.ClassNode;
import org.objectweb.asm.tree.FieldInsnNode;
import org.objectweb.asm.tree.InsnList;
import org.objectweb.asm.tree.IntInsnNode;
import org.objectweb.asm.tree.MethodInsnNode;
import org.objectweb.asm.tree.MethodNode;
import org.objectweb.asm.tree.VarInsnNode;

/** Separate driver instruments the complete Minecraft tick including all END subscribers. */
public final class Stability0402Transformer implements IClassTransformer {

    public byte[] transform(String name, String transformedName, byte[] bytes) {
        if (bytes == null) return bytes;
        if (Boolean.getBoolean("dgr0402.profileDrivers")
            && "net.minecraft.world.storage.SaveHandler".equals(transformedName)) {
            ClassNode node = new ClassNode();
            new ClassReader(bytes).accept(node, 0);
            for (Object value : node.methods) {
                MethodNode method = (MethodNode) value;
                if ("writePlayerData".equals(method.name) || "func_75753_a".equals(method.name))
                    requireCalls(method, "firePlayerSavingEvent", "firePlayerSavingEvent", 17);
            }
            ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
            node.accept(writer);
            return writer.toByteArray();
        }
        if (Boolean.getBoolean("dgr0402.profileDrivers")
            && "darkgrey.rpg.task.persistence.CanonicalTaskTransactionJournal".equals(transformedName))
            return transactionScopes(bytes, false);
        if (Boolean.getBoolean("dgr0402.profileDrivers")
            && "darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions$1".equals(transformedName))
            return transactionScopes(bytes, true);
        if (Boolean.getBoolean("dgr0402.profileDrivers")
            && "darkgrey.rpg.task.forge.CanonicalTaskPlayerTransactions".equals(transformedName))
            return playerTransactionScopes(bytes);
        if ("darkgrey.rpg.diagnostics.PlayerStatePacket".equals(transformedName)) return inspection(bytes, true);
        if ("darkgrey.rpg.client.gui.GuiPlayerStateInspection".equals(transformedName)) return inspection(bytes, false);
        if ("net.minecraft.client.Minecraft".equals(transformedName)) return clientFrames(bytes);
        if (!"net.minecraft.server.MinecraftServer".equals(transformedName)) return bytes;
        ClassNode node = new ClassNode();
        new ClassReader(bytes).accept(node, 0);
        int matched = 0;
        for (Object value : node.methods) {
            MethodNode method = (MethodNode) value;
            if (Boolean.getBoolean("dgr0402.profileDrivers")) {
                if (("saveAllWorlds".equals(method.name) || "func_71267_a".equals(method.name))
                    && "(Z)V".equals(method.desc)) bracket(method, 9);
                if ("()V".equals(method.desc) && ("tick".equals(method.name) || "func_71217_p".equals(method.name)))
                    requireCalls(method, "saveAllPlayerData", "func_72389_g", 10);
            }
            if (!"()V".equals(method.desc) || !("func_71217_p".equals(method.name) || "tick".equals(method.name)))
                continue;
            method.instructions.insert(call("begin"));
            for (AbstractInsnNode instruction = method.instructions.getFirst(); instruction
                != null; instruction = instruction.getNext()) {
                if (instruction.getOpcode() == Opcodes.RETURN)
                    method.instructions.insertBefore(instruction, call("end"));
            }
            matched++;
        }
        if (matched != 1) throw new IllegalStateException("Full tick sampler matched " + matched + " methods");
        ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        node.accept(writer);
        System.out.println("DGR0402_FULL_TICK_INSTRUMENTED=1");
        return writer.toByteArray();
    }

    private static byte[] inspection(byte[] bytes, boolean sending) {
        ClassNode node = new ClassNode();
        new ClassReader(bytes).accept(node, 0);
        int matched = 0;
        for (Object value : node.methods) {
            MethodNode method = (MethodNode) value;
            if (sending && "toBytes".equals(method.name) && "(Lio/netty/buffer/ByteBuf;)V".equals(method.desc)) {
                InsnList mark = new InsnList();
                mark.add(new VarInsnNode(Opcodes.ALOAD, 0));
                mark.add(new FieldInsnNode(Opcodes.GETFIELD, node.name, "kind", "I"));
                mark.add(new VarInsnNode(Opcodes.ALOAD, 0));
                mark.add(new FieldInsnNode(Opcodes.GETFIELD, node.name, "request", "J"));
                mark.add(
                    new MethodInsnNode(
                        Opcodes.INVOKESTATIC,
                        "darkgrey/rpg/persistence/Stability0402InteractionStats",
                        "sent",
                        "(IJ)V",
                        false));
                method.instructions.insert(mark);
                matched++;
            } else if (!sending && "accept".equals(method.name)
                && method.desc.startsWith("(J")
                && method.desc.endsWith(")V")) {
                    for (AbstractInsnNode instruction = method.instructions.getFirst(); instruction
                        != null; instruction = instruction.getNext()) if (instruction.getOpcode() == Opcodes.RETURN) {
                            InsnList mark = new InsnList();
                            mark.add(new VarInsnNode(Opcodes.ALOAD, 0));
                            mark.add(new VarInsnNode(Opcodes.LLOAD, 1));
                            mark.add(new VarInsnNode(Opcodes.ALOAD, 3));
                            mark.add(
                                new MethodInsnNode(
                                    Opcodes.INVOKESTATIC,
                                    "darkgrey/rpg/persistence/Stability0402InteractionStats",
                                    "applied",
                                    "(Ljava/lang/Object;JLjava/lang/Object;)V",
                                    false));
                            method.instructions.insertBefore(instruction, mark);
                        }
                    matched++;
                }
        }
        if (matched != 1) throw new IllegalStateException("Interaction sampler matched " + matched + " methods");
        ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        node.accept(writer);
        System.out.println("DGR0402_CLIENT_INTERACTION_INSTRUMENTED=" + (sending ? "request" : "GUI application"));
        return writer.toByteArray();
    }

    private static byte[] transactionScopes(byte[] bytes, boolean checkpoint) {
        ClassNode node = new ClassNode();
        new ClassReader(bytes).accept(node, 0);
        int matched = 0;
        for (Object value : node.methods) {
            MethodNode method = (MethodNode) value;
            if (checkpoint && "checkpoint".equals(method.name)) {
                requireCalls(method, "writePlayerData", "func_75753_a", 15);
                requireCalls(method, "getPlayerNBT", "getPlayerNBT", 16);
            }
            if (!checkpoint && "persist".equals(method.name)) {
                requireCalls(method, "compressJournal", "compressJournal", 12);
                requireCalls(method, "sync", "sync", 14);
            }
            int scope = checkpoint && "checkpoint".equals(method.name) ? 8
                : !checkpoint && "persist".equals(method.name) ? 6
                    : !checkpoint && ("read".equals(method.name) || "requireRecoveredPrevious".equals(method.name)) ? 7
                        : -1;
            if (scope < 0) continue;
            method.instructions.insert(scope(scope, "open"));
            for (AbstractInsnNode instruction = method.instructions.getFirst(); instruction
                != null; instruction = instruction.getNext()) {
                int opcode = instruction.getOpcode();
                if (opcode >= Opcodes.IRETURN && opcode <= Opcodes.RETURN)
                    method.instructions.insertBefore(instruction, scope(scope, "close"));
            }
            matched++;
        }
        if (matched != (checkpoint ? 1 : 3)) throw new IllegalStateException("Transaction profiler matched " + matched);
        ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        node.accept(writer);
        System.out.println("DGR0402_TRANSACTION_SCOPES=" + node.name + " count=" + matched);
        return writer.toByteArray();
    }

    private static byte[] playerTransactionScopes(byte[] bytes) {
        ClassNode node = new ClassNode();
        new ClassReader(bytes).accept(node, 0);
        int matched = 0;
        for (Object value : node.methods) {
            MethodNode method = (MethodNode) value;
            if ("validate".equals(method.name)) {
                bracket(method, 11);
                matched++;
            }
            if ("commit".equals(method.name)) {
                bracket(method, 13);
                matched++;
            }
        }
        if (matched != 2) throw new IllegalStateException("Player transaction profiler matched " + matched);
        ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        node.accept(writer);
        return writer.toByteArray();
    }

    private static void bracket(MethodNode method, int index) {
        method.instructions.insert(scope(index, "open"));
        for (AbstractInsnNode instruction = method.instructions.getFirst(); instruction
            != null; instruction = instruction.getNext())
            if (instruction.getOpcode() >= Opcodes.IRETURN && instruction.getOpcode() <= Opcodes.RETURN)
                method.instructions.insertBefore(instruction, scope(index, "close"));
    }

    private static void requireCalls(MethodNode method, String name, String mapped, int index) {
        int matched = 0;
        for (AbstractInsnNode instruction = method.instructions.getFirst(); instruction != null;) {
            AbstractInsnNode next = instruction.getNext();
            if (instruction instanceof MethodInsnNode) {
                MethodInsnNode call = (MethodInsnNode) instruction;
                if (name.equals(call.name) || mapped.equals(call.name)) {
                    matched++;
                    method.instructions.insertBefore(instruction, scope(index, "open"));
                    method.instructions.insert(instruction, scope(index, "close"));
                }
            }
            instruction = next;
        }
        if (matched != 1) throw new IllegalStateException("Profile call " + name + " matched " + matched);
    }

    private static InsnList scope(int index, String action) {
        InsnList code = new InsnList();
        code.add(new IntInsnNode(Opcodes.BIPUSH, index));
        code.add(
            new MethodInsnNode(
                Opcodes.INVOKESTATIC,
                "darkgrey/rpg/persistence/Stability0402TickStats",
                action,
                "(I)V",
                false));
        return code;
    }

    private static byte[] clientFrames(byte[] bytes) {
        ClassNode node = new ClassNode();
        new ClassReader(bytes).accept(node, 0);
        int matched = 0;
        for (Object value : node.methods) {
            MethodNode method = (MethodNode) value;
            if (!"()V".equals(method.desc)
                || !("runGameLoop".equals(method.name) || "func_71411_J".equals(method.name))) continue;
            method.instructions.insert(
                new MethodInsnNode(
                    Opcodes.INVOKESTATIC,
                    "darkgrey/rpg/persistence/Stability0402ClientStats",
                    "frame",
                    "()V",
                    false));
            matched++;
        }
        if (matched != 1) throw new IllegalStateException("Client frame sampler matched " + matched + " methods");
        ClassWriter writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        node.accept(writer);
        System.out.println("DGR0402_CLIENT_FRAME_INSTRUMENTED=1");
        return writer.toByteArray();
    }

    private static MethodInsnNode call(String name) {
        return new MethodInsnNode(
            Opcodes.INVOKESTATIC,
            "darkgrey/rpg/persistence/Stability0402TickStats",
            name,
            "()V",
            false);
    }
}
