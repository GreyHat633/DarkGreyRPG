package darkgrey.rpg.client.gui;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.stream.Stream;

import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/** Architecture fence: new screens cannot silently bypass the managed single-pass font path. */
public final class RuntimeFontPolicy0402Probe {

    public static void main(String[] args) throws Exception {
        require(
            DgrUiText.label("§l当前情况 §L文字§r §a颜色")
                .equals("当前情况 文字§r §a颜色"),
            "UI emphasis avoids duplicated strokes");
        require(
            DgrUiText.label("中文 A😀 123")
                .equals("中文 A😀 123"),
            "glyph content remains intact");
        darkgrey.rpg.client.session.PlayerUiPreferences.Theme previous = darkgrey.rpg.client.session.PlayerUiPreferences
            .theme();
        double opacity = darkgrey.rpg.client.session.PlayerUiPreferences.opacity();
        try {
            for (darkgrey.rpg.client.session.PlayerUiPreferences.Theme theme : darkgrey.rpg.client.session.PlayerUiPreferences.Theme
                .values()) {
                darkgrey.rpg.client.session.PlayerUiPreferences.setTheme(theme);
                DgrUiPalette.apply();
                require(
                    (DgrUiPalette.WINDOW_PANEL >>> 24) == 255 && DgrUiPalette.PANEL == DgrUiPalette.WINDOW_PANEL,
                    "Foreground text cannot bleed through any window theme");
                require(
                    (DgrUiPalette.dialoguePanel() >>> 24) == (int) Math.round(opacity * 255),
                    "Dialogue opacity preference remains effective");
            }
        } finally {
            darkgrey.rpg.client.session.PlayerUiPreferences.setTheme(previous);
            DgrUiPalette.apply();
        }
        Path root = Paths.get(args[0], "darkgrey/rpg");
        List<String> violations = new ArrayList<String>();
        final int[] classes = { 0 }, draws = { 0 };
        try (Stream<Path> files = Files.walk(root)) {
            for (Path file : (Iterable<Path>) files.filter(
                p -> p.toString()
                    .endsWith(".class"))::iterator) {
                classes[0]++;
                new ClassReader(Files.readAllBytes(file)).accept(new ClassVisitor(Opcodes.ASM5) {

                    private String name;

                    @Override
                    public void visit(int version, int access, String name, String signature, String parent,
                        String[] interfaces) {
                        this.name = name;
                    }

                    @Override
                    public MethodVisitor visitMethod(int access, String method, String descriptor, String signature,
                        String[] exceptions) {
                        return new MethodVisitor(Opcodes.ASM5) {

                            @Override
                            public void visitMethodInsn(int opcode, String owner, String target, String descriptor,
                                boolean isInterface) {
                                boolean rawFont = owner.equals("net/minecraft/client/gui/FontRenderer")
                                    && (target.startsWith("drawString") || target.equals("drawSplitString"));
                                if (rawFont) {
                                    draws[0]++;
                                    if (!name.equals("darkgrey/rpg/client/gui/DialogueFontDrawing"))
                                        violations.add(name + "." + method + " -> " + target);
                                }
                                boolean shadowHelper = target.equals("drawCenteredString")
                                    || target.equals("drawHoveringText")
                                    || (target.equals("drawString")
                                        && !owner.equals("net/minecraft/client/gui/FontRenderer"));
                                boolean rawField = owner.equals("net/minecraft/client/gui/GuiTextField")
                                    && target.equals("<init>")
                                    && !name.equals("darkgrey/rpg/client/gui/GuiDgrTextField");
                                boolean rawButton = owner.equals("net/minecraft/client/gui/GuiButton")
                                    && target.equals("<init>")
                                    && !(name.endsWith("/GuiRpgButton") || name.endsWith("/GuiModernButton")
                                        || name.endsWith("/GuiWrappedChoiceButton"));
                                if (shadowHelper || rawField || rawButton)
                                    violations.add(name + "." + method + " -> " + owner + "." + target);
                            }
                        };
                    }
                }, ClassReader.SKIP_DEBUG | ClassReader.SKIP_FRAMES);
            }
        }
        require(
            classes[0] > 100 && draws[0] == 1,
            "production classes and single managed font entry actually inspected");
        require(violations.isEmpty(), "Unmanaged text paths: " + violations);
        System.out.println(
            "RuntimeFontPolicy0402Probe PASS: " + classes[0]
                + " production classes, one raw font entry, no unscoped text/button/field helpers");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new IllegalStateException(message);
    }
}
