package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FilterInputStream;
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

/** Strict metadata view of a DGRS archive. */
public final class DgrsArchiveReader {

    public static final long MAX_ENTRY_BYTES = 64L * 1024L * 1024L;
    public static final int MAX_ENTRY_COUNT = 4096;
    public static final long MAX_TOTAL_UNCOMPRESSED_BYTES = 256L * 1024L * 1024L;
    public static final int READ_BUFFER_BYTES = 32768;
    private static final String MANIFEST_ENTRY = "manifest.json";
    private static final String PROJECT_ENTRY = "project.json";

    private final File sourceArchive;
    private final List<String> entryNames;
    private final Map<String, EntryMetadata> entries;

    public DgrsArchiveReader(File archive) throws ProjectLoadException {
        if (archive == null) throw new ProjectLoadException("DGRS archive cannot be null");
        sourceArchive = archive.getAbsoluteFile();
        if (!sourceArchive.isFile()) throw new ProjectLoadException("DGRS archive does not exist: " + sourceArchive);
        Map<String, EntryMetadata> loaded = new HashMap<String, EntryMetadata>();
        List<String> names = new ArrayList<String>();
        Map<String, String> normalizedNames = new HashMap<String, String>();
        try (ZipFile zip = new ZipFile(sourceArchive)) {
            Enumeration<? extends ZipEntry> enumeration = zip.entries();
            int entryCount = 0;
            long totalUncompressedBytes = 0L;
            while (enumeration.hasMoreElements()) {
                ZipEntry entry = enumeration.nextElement();
                entryCount++;
                if (entryCount > MAX_ENTRY_COUNT)
                    throw failure("DGRS archive exceeds the maximum entry count of " + MAX_ENTRY_COUNT, null);
                String name = entry.getName();
                validateEntryPath(name, entry);
                String normalized = name.toLowerCase(Locale.ROOT);
                String previous = normalizedNames.put(normalized, name);
                if (previous != null) throw failure(
                    "Duplicate normalized archive entry '" + name + "' (already '" + previous + "')",
                    null);
                long declared = entry.getSize();
                if (declared > MAX_ENTRY_BYTES
                    || (declared >= 0L && declared > MAX_TOTAL_UNCOMPRESSED_BYTES - totalUncompressedBytes))
                    throw failure("DGRS entry exceeds its declared size: " + name, null);
                long actual = consumeEntry(zip, entry, totalUncompressedBytes);
                totalUncompressedBytes += actual;
                names.add(name);
                loaded.put(name, new EntryMetadata(name, actual));
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
        entries = Collections.unmodifiableMap(new HashMap<String, EntryMetadata>(loaded));
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

    public long getEntrySize(String path) throws ProjectLoadException {
        EntryMetadata metadata = entries.get(path);
        if (metadata == null) throw new ProjectLoadException("DGRS entry is missing: " + path);
        return metadata.size;
    }

    /** Opens one entry without retaining its bytes. Caller must close it. */
    public InputStream openStream(final String path) throws ProjectLoadException {
        if (path == null || path.length() == 0) throw new ProjectLoadException("DGRS entry path is required");
        if (!entries.containsKey(path)) throw new ProjectLoadException("DGRS entry is missing: " + path);
        try {
            final ZipFile zip = new ZipFile(sourceArchive);
            InputStream input = zip.getInputStream(zip.getEntry(path));
            return new FilterInputStream(input) {

                private boolean closed;

                @Override
                public void close() throws IOException {
                    if (closed) return;
                    closed = true;
                    try {
                        super.close();
                    } finally {
                        zip.close();
                    }
                }
            };
        } catch (IOException | RuntimeException exception) {
            throw failure("Cannot open DGRS entry: " + path, exception);
        }
    }

    public byte[] readBytes(String path) throws ProjectLoadException {
        try (InputStream input = openStream(path)) {
            java.io.ByteArrayOutputStream output = new java.io.ByteArrayOutputStream();
            byte[] buffer = new byte[READ_BUFFER_BYTES];
            int count;
            long total = 0L;
            while ((count = input.read(buffer)) != -1) {
                total += count;
                if (total > MAX_ENTRY_BYTES) throw failure("DGRS entry exceeds the maximum size: " + path, null);
                output.write(buffer, 0, count);
            }
            return output.toByteArray();
        } catch (IOException exception) {
            throw failure("Cannot read DGRS entry: " + path, exception);
        }
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

    private static long consumeEntry(ZipFile zip, ZipEntry entry, long prior) throws IOException, ProjectLoadException {
        long declared = entry.getSize();
        long remaining = MAX_TOTAL_UNCOMPRESSED_BYTES - prior;
        long total = 0L;
        try (InputStream input = zip.getInputStream(entry)) {
            byte[] buffer = new byte[READ_BUFFER_BYTES];
            int count;
            while ((count = input.read(buffer)) != -1) {
                total += count;
                if (total > MAX_ENTRY_BYTES || total > remaining) throw failure(
                    "DGRS archive exceeds its uncompressed size limit at '" + entry.getName() + "'",
                    null);
            }
        }
        if (declared >= 0L && total != declared) throw failure("DGRS entry size mismatch: " + entry.getName(), null);
        return total;
    }

    private static void validateEntryPath(String name, ZipEntry entry) throws ProjectLoadException {
        if (name == null || name.length() == 0) throw failure("DGRS archive contains an empty entry path", null);
        if (entry.isDirectory() || name.endsWith("/"))
            throw failure("DGRS archive contains a directory entry: " + name, null);
        if (name.indexOf('\\') >= 0) throw failure("DGRS archive entry contains a backslash: " + name, null);
        if (name.startsWith("/") || name.indexOf(':') >= 0)
            throw failure("DGRS archive entry is absolute or drive-qualified: " + name, null);
        for (String segment : name.split("/", -1))
            if (segment.length() == 0 || ".".equals(segment) || "..".equals(segment))
                throw failure("DGRS archive entry contains an unsafe path segment: " + name, null);
    }

    private static ProjectLoadException failure(String message, Throwable cause) {
        return cause == null ? new ProjectLoadException(message) : new ProjectLoadException(message, cause);
    }

    public static final class EntryMetadata {

        private final String name;
        private final long size;

        EntryMetadata(String name, long size) {
            this.name = name;
            this.size = size;
        }

        public String getName() {
            return name;
        }

        public long getSize() {
            return size;
        }
    }
}
