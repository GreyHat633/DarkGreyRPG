package darkgrey.rpg.project.packages;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Enumeration;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

import darkgrey.rpg.project.ProjectLoadException;

/**
 * Detached, read-only view of one DGRS archive.
 *
 * <p>
 * The ZIP file is only held while the archive is opened. Every entry is
 * validated and copied into memory before this reader is returned; callers
 * therefore never retain a ZIP stream or a file handle.
 * </p>
 */
public final class DgrsArchiveReader {

    /** Safety bound for one archive entry, including entries with unknown size. */
    private static final long MAX_ENTRY_BYTES = 64L * 1024L * 1024L;
    private static final String MANIFEST_ENTRY = "manifest.json";
    private static final String PROJECT_ENTRY = "project.json";

    private final File sourceArchive;
    private final List<String> entryNames;
    private final Map<String, byte[]> entries;

    public DgrsArchiveReader(File archive) throws ProjectLoadException {
        if (archive == null) throw new ProjectLoadException("DGRS archive cannot be null");
        sourceArchive = archive.getAbsoluteFile();
        if (!sourceArchive.isFile()) throw new ProjectLoadException("DGRS archive does not exist: " + sourceArchive);

        Map<String, byte[]> loaded = new HashMap<String, byte[]>();
        List<String> names = new ArrayList<String>();
        Map<String, String> normalizedNames = new HashMap<String, String>();
        try (ZipFile zip = new ZipFile(sourceArchive)) {
            Enumeration<? extends ZipEntry> enumeration = zip.entries();
            while (enumeration.hasMoreElements()) {
                ZipEntry entry = enumeration.nextElement();
                String name = entry.getName();
                validateEntryPath(name, entry);
                String normalized = name.toLowerCase(Locale.ROOT);
                String previous = normalizedNames.put(normalized, name);
                if (previous != null) throw failure(
                    "Duplicate normalized archive entry '" + name + "' (already '" + previous + "')",
                    null);
                byte[] content = readEntry(zip, entry);
                names.add(name);
                loaded.put(name, content);
            }
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (IOException | RuntimeException exception) {
            throw failure("Invalid DGRS archive " + sourceArchive, exception);
        }
        if (!loaded.containsKey(MANIFEST_ENTRY))
            throw failure("DGRS archive is missing manifest.json: " + sourceArchive, null);
        if (!loaded.containsKey(PROJECT_ENTRY))
            throw failure("DGRS archive is missing project.json: " + sourceArchive, null);

        entryNames = Collections.unmodifiableList(new ArrayList<String>(names));
        entries = Collections.unmodifiableMap(new HashMap<String, byte[]>(loaded));
    }

    public static DgrsArchiveReader open(File archive) throws ProjectLoadException {
        return new DgrsArchiveReader(archive);
    }

    public File getSourceArchive() {
        return sourceArchive;
    }

    public File getArchiveFile() {
        return sourceArchive;
    }

    public String getSourceIdentity() {
        return sourceArchive.getPath();
    }

    public List<String> getEntryNames() {
        return entryNames;
    }

    public List<String> entryNames() {
        return entryNames;
    }

    public boolean contains(String path) {
        return path != null && entries.containsKey(path);
    }

    public byte[] readBytes(String path) throws ProjectLoadException {
        if (path == null || path.length() == 0) throw new ProjectLoadException("DGRS entry path is required");
        byte[] content = entries.get(path);
        if (content == null) throw new ProjectLoadException("DGRS entry is missing: " + path);
        return content.clone();
    }

    public String readUtf8(String path) throws ProjectLoadException {
        try {
            return StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT)
                .decode(ByteBuffer.wrap(readBytes(path)))
                .toString();
        } catch (CharacterCodingException exception) {
            throw new ProjectLoadException("DGRS entry is not valid UTF-8: " + path, exception);
        }
    }

    private static void validateEntryPath(String name, ZipEntry entry) throws ProjectLoadException {
        if (name == null || name.length() == 0) throw failure("DGRS archive contains an empty entry path", null);
        if (entry.isDirectory() || name.endsWith("/"))
            throw failure("DGRS archive contains a directory entry: " + name, null);
        if (name.indexOf('\\') >= 0) throw failure("DGRS archive entry contains a backslash: " + name, null);
        if (name.startsWith("/") || name.indexOf(':') >= 0)
            throw failure("DGRS archive entry is absolute or drive-qualified: " + name, null);
        String[] segments = name.split("/", -1);
        for (String segment : segments) {
            if (segment.length() == 0 || ".".equals(segment) || "..".equals(segment))
                throw failure("DGRS archive entry contains an unsafe path segment: " + name, null);
        }
    }

    private static byte[] readEntry(ZipFile zip, ZipEntry entry) throws IOException, ProjectLoadException {
        long declared = entry.getSize();
        if (declared > MAX_ENTRY_BYTES) throw failure("DGRS entry exceeds the maximum size: " + entry.getName(), null);
        long limit = declared >= 0 ? declared : MAX_ENTRY_BYTES;
        int initial = declared >= 0 && declared <= Integer.MAX_VALUE ? (int) declared : 0;
        ByteArrayOutputStream output = new ByteArrayOutputStream(initial);
        InputStream input = zip.getInputStream(entry);
        try {
            byte[] buffer = new byte[8192];
            long total = 0;
            int count;
            while ((count = input.read(buffer)) != -1) {
                if (count > limit - total)
                    throw failure("DGRS entry exceeds its declared size: " + entry.getName(), null);
                output.write(buffer, 0, count);
                total += count;
            }
            if (declared >= 0 && total != declared) throw failure("DGRS entry size mismatch: " + entry.getName(), null);
            return output.toByteArray();
        } finally {
            input.close();
        }
    }

    private static ProjectLoadException failure(String message, Throwable cause) {
        return cause == null ? new ProjectLoadException(message) : new ProjectLoadException(message, cause);
    }
}
