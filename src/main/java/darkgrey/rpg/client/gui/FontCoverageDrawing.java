package darkgrey.rpg.client.gui;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.FloatBuffer;

import org.lwjgl.BufferUtils;
import org.lwjgl.opengl.GL11;
import org.lwjgl.opengl.GL13;
import org.lwjgl.opengl.GL20;
import org.lwjgl.opengl.GLContext;

/** Integrates the original bitmap over screen pixels, without changing glyph geometry or advances. */
final class FontCoverageDrawing {

    private static FloatBuffer projection;
    private static int program, sampler;
    private static boolean unavailable;

    private FontCoverageDrawing() {}

    static boolean fractional(double scale, int factor) {
        double texelPixels = scale * Math.max(1, factor) / 2;
        return Math.abs(texelPixels - Math.rint(texelPixels)) > 0.00001;
    }

    /** -1 means the original renderer owns the incoming state. */
    static int begin(double scale, int factor, String text) {
        if (!fractional(scale, factor) || unavailable || !GLContext.getCapabilities().OpenGL30) return -1;
        // Decorations use untextured vanilla geometry; leave that path and foreign shaders intact.
        if (text.contains("\u00a7n") || text.contains("\u00a7N")
            || text.contains("\u00a7m")
            || text.contains("\u00a7M")) return -1;
        int previous = GL11.glGetInteger(GL20.GL_CURRENT_PROGRAM);
        if (previous != 0) return -1;
        if (projection == null) projection = BufferUtils.createFloatBuffer(16);
        projection.clear();
        GL11.glGetFloat(GL11.GL_PROJECTION_MATRIX, projection);
        if (Math.abs(projection.get(15) - 1) > 0.00001) return -1; // World billboards keep their renderer.
        if (program == 0) {
            try {
                create();
            } catch (IOException | RuntimeException error) {
                unavailable = true;
                darkgrey.rpg.DarkGreyRpg.LOG.warn("Font pixel coverage unavailable; retaining bitmap rendering", error);
                return -1;
            }
        }
        GL20.glUseProgram(program);
        GL20.glUniform1i(sampler, GL11.glGetInteger(GL13.GL_ACTIVE_TEXTURE) - GL13.GL_TEXTURE0);
        GL11.glEnable(GL11.GL_BLEND);
        GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);
        // FontRenderer enables alpha testing itself; retain every nonzero coverage instead of clipping at 10%.
        GL11.glAlphaFunc(GL11.GL_GREATER, 0);
        return previous;
    }

    static void end(int previous) {
        if (previous >= 0) GL20.glUseProgram(previous);
    }

    private static void create() throws IOException {
        int vertex = 0, fragment = 0, linked = 0;
        try {
            vertex = compile(GL20.GL_VERTEX_SHADER, "font_coverage.vert");
            fragment = compile(GL20.GL_FRAGMENT_SHADER, "font_coverage.frag");
            linked = GL20.glCreateProgram();
            GL20.glAttachShader(linked, vertex);
            GL20.glAttachShader(linked, fragment);
            GL20.glLinkProgram(linked);
            if (GL20.glGetProgrami(linked, GL20.GL_LINK_STATUS) == GL11.GL_FALSE)
                throw new IllegalStateException(GL20.glGetProgramInfoLog(linked, 2048));
            sampler = GL20.glGetUniformLocation(linked, "fontTexture");
            if (sampler < 0) throw new IllegalStateException("Font coverage sampler missing");
            program = linked;
            linked = 0;
        } finally {
            if (vertex != 0) GL20.glDeleteShader(vertex);
            if (fragment != 0) GL20.glDeleteShader(fragment);
            if (linked != 0) GL20.glDeleteProgram(linked);
        }
    }

    private static int compile(int type, String name) throws IOException {
        String source;
        try (InputStream stream = FontCoverageDrawing.class
            .getResourceAsStream("/assets/darkgrey_rpg/shaders/" + name)) {
            if (stream == null) throw new IOException("Missing font coverage shader " + name);
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            byte[] buffer = new byte[1024];
            for (int count; (count = stream.read(buffer)) != -1;) bytes.write(buffer, 0, count);
            source = new String(bytes.toByteArray(), java.nio.charset.StandardCharsets.UTF_8);
        }
        int shader = GL20.glCreateShader(type);
        GL20.glShaderSource(shader, source);
        GL20.glCompileShader(shader);
        if (GL20.glGetShaderi(shader, GL20.GL_COMPILE_STATUS) == GL11.GL_FALSE) {
            String error = GL20.glGetShaderInfoLog(shader, 2048);
            GL20.glDeleteShader(shader);
            throw new IllegalStateException(error);
        }
        return shader;
    }
}
